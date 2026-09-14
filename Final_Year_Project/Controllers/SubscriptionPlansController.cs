using AngleSharp.Dom;
using AngleSharp.Io;
using Final_Year_Project.Enums;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Models.SubscriptionPlans;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Supabase.Gotrue;
using System.Reactive.Joins;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using static Supabase.Postgrest.Constants;

namespace Final_Year_Project.Controllers
{
    [Authorize(Policy = "NonAdminOnly")]
    public class SubscriptionPlansController : Controller
    {
        private readonly SupabaseService _supabaseService;
        private readonly LocalStorageService _storageService;
        private readonly IPaymentService _paymentService;
        private readonly EnumService _enumService;

        public SubscriptionPlansController(SupabaseService supabaseService, LocalStorageService storageService, IPaymentService paymentService, EnumService enumService)
        {
            _supabaseService = supabaseService;
            _storageService = storageService;
            _paymentService = paymentService;
            _enumService = enumService;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateCheckoutSession(int planId)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");
            var uid = int.Parse(uidValue);

            var client = _supabaseService.GetClient();
            var tomorrow = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff+00");

            var subscribeResponseTask = await client
            .From<Subscription>()
            .Filter("UId", Operator.Equals, uid)
            .Filter("end_date", Operator.GreaterThan, tomorrow)
            .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionStatus.active))
            .Filter("payment_status", Operator.Equals, _enumService.ToStringValue(paymentStatus.paid))
            .Get();

            var currentPlan = await client
            .From<SubscriptionPlan>()
            .Filter("SPId", Operator.Equals, planId)
            .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionPlanStatus.active))
            .Single();

            if (currentPlan == null || uid == currentPlan.UId)
                return View("NotFound");

            // No discount/upgrade benefits allowed. Force single active subscription per creator.
            var subscribeResponse = subscribeResponseTask.Models.ToList();

            if (subscribeResponse != null)
            {
                foreach (var s in subscribeResponse)
                {
                    var plan = await client
                        .From<SubscriptionPlan>()
                        .Filter("SPId", Operator.Equals, s.SPId)
                        .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionPlanStatus.active))
                        .Single();

                    // If the user already has an active subscription for the same creator (plan owner), block subscribing again
                    if (plan != null && plan.UId == currentPlan.UId)
                    {
                        TempData["ErrorMessage"] = "You already have an active subscription for this creator. You can only subscribe to one plan per creator.";
                        return Redirect($"/SubscriptionPlans/{planId}");
                    }
                }
            }

            // Always charge full price, no discounts
            decimal total = currentPlan.Price;

            var domain = $"{Request.Scheme}://{Request.Host}/";
            var session = _paymentService.CreateCheckoutSession(total, domain, "SubscriptionPlans/Success", "SubscriptionPlans/Cancel");

            var subscription = new Subscription
            {
                UId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
                SPId = planId,
                CreatedAt = DateTime.UtcNow,
                TotalAmount = total,
                ServiceTax = total * 0.12m,
                Status = _enumService.ToStringValue(subscriptionStatus.pending),
                PaymentStatus = _enumService.ToStringValue(paymentStatus.unpaid)
            };
            var inserted = await _supabaseService.GetClient().From<Subscription>().Insert(subscription);
            var newSubscription = inserted.Models.FirstOrDefault();

            HttpContext.Session.SetString("SubscribeId", newSubscription!.SId.ToString());
            return Redirect(session.Url);
        }

        public async Task<IActionResult> Success()
        {
            var sId = Convert.ToInt32(HttpContext.Session.GetString("SubscribeId"));

            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var client = _supabaseService.GetClient();
            var uid = int.Parse(uidValue!);

            var subscription = await client.From<Subscription>().Where(s => s.SId == sId && s.UId == uid).Single();
            if (subscription == null || subscription.Status == _enumService.ToStringValue(subscriptionStatus.active) && subscription.PaymentStatus == _enumService.ToStringValue(paymentStatus.paid))
                return View("NotFound");

            var newPlan = await client.From<SubscriptionPlan>()
                          .Where(p => p.SPId == subscription.SPId)
                          .Single();

            var existingSubsTask = await client
            .From<Subscription>()
            .Filter("UId", Operator.Equals, uid)
            .Filter("end_date", Operator.GreaterThan, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff+00"))
            .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionStatus.active))
            .Filter("payment_status", Operator.Equals, _enumService.ToStringValue(paymentStatus.paid))
            .Get();

            var existingSubs = existingSubsTask.Models.ToList();
            // Do not modify existing subscriptions for the same creator — users are not allowed
            // to hold more than one active subscription per artist, and CreateCheckoutSession
            // ensures this is enforced before creating a new subscription.

            subscription.Status = _enumService.ToStringValue(subscriptionStatus.active);
            subscription.PaymentStatus = _enumService.ToStringValue(paymentStatus.paid);
            subscription.StartDate = DateTime.UtcNow;
            subscription.EndDate = DateTime.UtcNow.AddMonths(1);
            subscription.PaymentDate = DateTime.UtcNow;
            subscription.IsRenewal = true;

            await client.From<Subscription>().Update(subscription);

            var plan = await client.From<SubscriptionPlan>().Where(p => p.SPId == subscription.SPId).Single();
            if (plan == null)
                RedirectToAction("Index", "Home");

            var user = await client.From<Users>().Where(u => u.Uid == plan!.UId).Single();
            if (user == null)
                RedirectToAction("Index", "Home");

            TempData["SubscribeMessage"] = "You are now subscribed " + user!.Nickname + "!";
            return Redirect($"/Profile/{plan!.UId}");
        }

        public async Task<IActionResult> Cancel()
        {
            var sId = Convert.ToInt32(HttpContext.Session.GetString("SubscribeId"));

            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var client = _supabaseService.GetClient();
            var uid = int.Parse(uidValue!);

            var subscription = await client.From<Subscription>().Where(s => s.SId == sId && s.UId == uid).Single();
            if (subscription == null || subscription.Status == _enumService.ToStringValue(subscriptionStatus.active) && subscription.PaymentStatus == _enumService.ToStringValue(paymentStatus.paid))
                return View("NotFound");

            subscription.Status = _enumService.ToStringValue(subscriptionStatus.cancelled);
            subscription.PaymentStatus = _enumService.ToStringValue(paymentStatus.unpaid);

            await client.From<Subscription>().Update(subscription);

            var plan = await client.From<SubscriptionPlan>().Where(p => p.SPId == subscription.SPId).Single();
            if (plan == null)
                RedirectToAction("Index", "Home");

            return Redirect($"/Profile/{plan!.UId}");
        }

        [Authorize]
        [Route("SubscriptionPlans/{id:int}")]
        public async Task<IActionResult> Plans(int id)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var client = _supabaseService.GetClient();
            var uid = int.Parse(uidValue!);

            var planResponse = await client
                .From<SubscriptionPlan>()
                .Filter("SPId", Operator.Equals, id)
                .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionPlanStatus.active))
                .Single();


            if (planResponse == null)
                return View("NotFound");

            if (planResponse.UId == uid)
                return Redirect($"/Profile/{uidValue}");

            var userResponse = await client
                .From<Users>()
                .Filter("UId", Operator.Equals, planResponse.UId)
                .Filter("status", Operator.Equals, _enumService.ToStringValue(usersStatus.active))
                .Single();

            if (userResponse == null)
                return View("NotFound");

            var tomorrow = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff+00");

            bool? isRenewal = null;
            bool canSubscribe = true;
            bool subscribed = false;
            var subscribeResponseTask = await client
            .From<Subscription>()
            .Filter("UId", Operator.Equals, uid)
            .Filter("end_date", Operator.GreaterThan, tomorrow)
            .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionStatus.active))
            .Filter("payment_status", Operator.Equals, _enumService.ToStringValue(paymentStatus.paid))
            .Get();

            var subscribeResponse = subscribeResponseTask.Models.ToList();

            if (subscribeResponse != null)
            {
                foreach (var s in subscribeResponse)
                {
                    var subscribePlanResponse = await client
                    .From<SubscriptionPlan>()
                    .Filter("SPId", Operator.Equals, s.SPId)
                    .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionPlanStatus.active))
                    .Single();

                    if (s.SPId == planResponse.SPId)
                    {

                        isRenewal = s.IsRenewal;
                        subscribed = true;
                    }
                    else if (subscribePlanResponse != null)
                    {
                        // If the existing active subscription is for a plan by the same creator, disallow subscribing
                        if (subscribePlanResponse.UId == planResponse.UId)
                        {
                            canSubscribe = false;
                        }
                    }
                }
            }

            var vm = new SubscriptionPlanViewModel
            {
                SPId = planResponse.SPId,
                UId = planResponse.UId,
                Title = planResponse.Title ?? "",
                Description = planResponse.Description ?? "",
                Price = planResponse.Price,
                CoverImageUrl = string.IsNullOrEmpty(planResponse.Image) ? null : _storageService.BuildFileUrl("subscriptionPlan_cover", planResponse.Image),
                ProfileImageUrl = string.IsNullOrEmpty(userResponse.ProfilePic) ? null : _storageService.BuildFileUrl("profile_pic", userResponse.ProfilePic),
                Nickname = userResponse.Nickname ?? "",
                Subscribed = subscribed,
                IsRenewal = isRenewal,
                CanSubscribe = canSubscribe,
                // upgrade/discount removed
            };

            return View(vm);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PlansCancel(int id)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var client = _supabaseService.GetClient();
            var uid = int.Parse(uidValue!);

            var planResponse = await client
            .From<SubscriptionPlan>()
            .Filter("SPId", Operator.Equals, id)
            .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionPlanStatus.active))
            .Single();

            if (planResponse == null)
                return View("NotFound");

            if (planResponse.UId == uid)
                return Redirect($"/Profile/{uidValue}");

            var userResponse = await client
                .From<Users>()
                .Filter("UId", Operator.Equals, planResponse.UId)
                .Filter("status", Operator.Equals, _enumService.ToStringValue(usersStatus.active))
                .Single();

            if (userResponse == null)
                return View("NotFound");

            var tomorrow = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff+00");
            var subscribeResponse = await client
                .From<Subscription>()
                .Filter("UId", Operator.Equals, uid)
                .Filter("SPId", Operator.Equals, planResponse.SPId)
                .Filter("end_date", Operator.GreaterThan, tomorrow)
                .Filter("is_renewal", Operator.Equals, "true")
                .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionStatus.active))
                .Filter("payment_status", Operator.Equals, _enumService.ToStringValue(paymentStatus.paid))
                .Single();

            if (subscribeResponse == null)
                return View("NotFound");

            subscribeResponse.IsRenewal = false;

            await client.From<Subscription>().Update(subscribeResponse);

            TempData["SubscribeCancelMessage"] = "You have unsubscribed " + userResponse.Nickname + ".";
            return Redirect($"/Profile/{planResponse.UId}");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PlansReSubscribe(int id)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var client = _supabaseService.GetClient();
            var uid = int.Parse(uidValue!);

            var planResponse = await client
            .From<SubscriptionPlan>()
            .Filter("SPId", Operator.Equals, id)
            .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionPlanStatus.active))
            .Single();

            if (planResponse == null)
                return View("NotFound");

            if (planResponse.UId == uid)
                return Redirect($"/Profile/{uidValue}");

            var userResponse = await client
                .From<Users>()
                .Filter("UId", Operator.Equals, planResponse.UId)
                .Filter("status", Operator.Equals, _enumService.ToStringValue(usersStatus.active))
                .Single();

            if (userResponse == null)
                return View("NotFound");

            var tomorrow = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff+00");
            var subscribeResponse = await client
                .From<Subscription>()
                .Filter("UId", Operator.Equals, uid)
                .Filter("SPId", Operator.Equals, planResponse.SPId)
                .Filter("end_date", Operator.GreaterThan, tomorrow)
                .Filter("is_renewal", Operator.Equals, "false")
                .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionStatus.active))
                .Filter("payment_status", Operator.Equals, _enumService.ToStringValue(paymentStatus.paid))
                .Single();

            if (subscribeResponse == null)
                return View("NotFound");

            subscribeResponse.IsRenewal = true;

            await client.From<Subscription>().Update(subscribeResponse);

            TempData["SubscribeMessage"] = "You are now subscribed " + userResponse.Nickname + "!";
            return Redirect($"/Profile/{planResponse.UId}");
        }

        [Authorize]
        [Route("SubscriptionPlans/add")]
        public IActionResult PlansAdd()
        {
            var redirect = RedirectIfNotArtist();
            if (redirect != null)
                return redirect;

            return View();
        }

        [Authorize]
        [HttpPost]
        [Route("SubscriptionPlans/add")]
        public async Task<IActionResult> PlansAdd(PlansAddEditVM model)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(uidValue))
            {
                return RedirectToAction("Login", "Account");
            }

            var redirect = RedirectIfNotArtist();
            if (redirect != null)
                return redirect;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var uid = int.Parse(uidValue!);

            var client = _supabaseService.GetClient();

            var existingPlans = await _supabaseService.GetClient()
                                .From<SubscriptionPlan>()
                                .Filter("UId", Operator.Equals, uid)
                                .Filter("status", Operator.Equals, "active")
                                .Filter("price", Operator.Equals, (float)model.Price)
                                .Get();


            if (existingPlans.Models.Count > 0)
            {
                ModelState.AddModelError("Price", "A plan with this amount already exists.");
                return View(model);
            }

            string? fileName = null;
            if (model.CoverImage != null && model.CoverImage.Length > 0)
                fileName = await _storageService.SaveAsync(model.CoverImage, "subscriptionPlan_cover", new[] { "image/jpeg", "image/png" });

            var plan = new SubscriptionPlan
            {
                CreatedAt = DateTime.UtcNow,
                UId = int.Parse(uidValue),
                Title = model.Title,
                Description = model.Description ?? "",
                Price = model.Price,
                Image = fileName ?? ""
            };

            if (existingPlans.Models.Count > 0)
            {
                ModelState.AddModelError("Price", "A plan with this amount already exists.");
                return View(model);
            }

            await _supabaseService.GetClient()
                .From<SubscriptionPlan>()
                .Insert(plan);

            TempData["SuccessMessage"] = "Plan added";
            return Redirect($"/Profile/{uidValue}/plans");
        }

        [Authorize]
        [Route("SubscriptionPlans/{id:int}/edit")]
        public async Task<IActionResult> PlansEdit(int id)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");
            var uid = int.Parse(uidValue!);

            var redirect = RedirectIfNotArtist();
            if (redirect != null)
                return redirect;

            var plan = await _supabaseService.GetClient()
                                .From<SubscriptionPlan>()
                                .Filter("SPId", Operator.Equals, id)
                                .Single();
            if (plan == null || plan.Status != "active" || plan.UId != uid)
                return View("NotFound");

            var vm = new PlansAddEditVM
            {
                Title = plan.Title ?? "",
                Price = plan.Price,
                Description = plan.Description,
                SPId = plan.SPId
            };

            string? imageUrl = null;
            if (!string.IsNullOrEmpty(plan.Image))
                imageUrl = _storageService.BuildFileUrl("subscriptionPlan_cover", plan.Image);

            vm.CoverImageUrl = imageUrl;

            return View(vm);
        }

        [Authorize]
        [HttpPost]
        [Route("SubscriptionPlans/{id:int}/edit")]
        public async Task<IActionResult> PlansEdit(int id, PlansAddEditVM model)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var redirect = RedirectIfNotArtist();
            if (redirect != null)
                return redirect;

            if (!ModelState.IsValid)
                return View(model);

            var uid = int.Parse(uidValue!);
            var client = _supabaseService.GetClient();

            var existingPlans = await _supabaseService.GetClient()
                    .From<SubscriptionPlan>()
                    .Filter("SPId", Operator.Equals, id)
                    .Filter("UId", Operator.Equals, uid)
                    .Filter("status", Operator.Equals, "active")
                    .Single();

            if (existingPlans == null || existingPlans.Status != "active")
                return Redirect($"/Profile/{uidValue}/plans");

            var plan = existingPlans;

            if (model.CoverImage != null && model.CoverImage.Length > 0)
            {
                if (!string.IsNullOrEmpty(plan.Image))
                {
                    _storageService.Delete("subscriptionPlan_cover", plan.Image);
                }

                var newFileName = await _storageService.SaveAsync(model.CoverImage, "subscriptionPlan_cover", new[] { "image/jpeg", "image/png" });
                plan.Image = newFileName ?? "";
            }
            else if (model.RemoveCoverImage)
            {
                if (!string.IsNullOrEmpty(plan.Image))
                    _storageService.Delete("subscriptionPlan_cover", plan.Image);

                plan.Image = "";
            }

            var updateResponse = await client
                .From<SubscriptionPlan>()
                .Where(p => p.SPId == id && p.UId == uid)
                .Set(p => new KeyValuePair<object, object?>(p.Title, model.Title))
                .Set(p => new KeyValuePair<object, object?>(p.Description!, model.Description ?? ""))
                .Set(p => new KeyValuePair<object, object?>(p.Image!, plan.Image ?? ""))
                .Update();

            if (updateResponse.Models.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Failed to update the plan. Please try again.");
                return View(model);
            }

            TempData["SuccessMessage"] = "Plan updated";
            return Redirect($"/Profile/{uidValue}/plans");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PlansDelete(int id)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var redirect = RedirectIfNotArtist();
            if (redirect != null)
                return redirect;

            var uid = int.Parse(uidValue!);

            var client = _supabaseService.GetClient();

            var plan = await client
                .From<SubscriptionPlan>()
                .Filter("SPId", Operator.Equals, id)
                .Filter("UId", Operator.Equals, uid)
                .Single();

            if (plan == null)
                return Redirect($"/Profile/{uidValue}/plans");

            // Clear SPId from all posts associated with this subscription plan
            var relatedPosts = await client
                .From<Posts>()
                .Filter("SPId", Operator.Equals, id)
                .Get();

            if (relatedPosts.Models.Any())
            {
                foreach (var post in relatedPosts.Models)
                {
                    post.SPId = null;
                    post.UpdatedAt = DateTime.UtcNow;
                    
                    await client
                        .From<Posts>()
                        .Where(p => p.PostId == post.PostId)
                        .Update(post);
                }
            }

            var response = await client
                .From<SubscriptionPlan>()
                .Where(p => p.SPId == id && p.UId == uid)
                .Set(p => new KeyValuePair<object, object?>(p.Status, "deleted"))
                .Update();

            if (response.Models.Count == 0)
                return Redirect($"/Profile/{uidValue}/plans");
            else
            {
                if (!string.IsNullOrEmpty(plan.Image))
                    _storageService.Delete("subscriptionPlan_cover", plan.Image);
            }

            TempData["SuccessMessage"] = "Plan deleted";
            return Redirect($"/Profile/{uidValue}/plans");
        }

        private IActionResult? RedirectIfNotArtist()
        {
            var user = User;

            // If there is no authenticated user or user is not in the artist role, redirect
            if (user == null || !user.IsInRole(_enumService.ToStringValue(usersRole.artist)))
                return RedirectToAction("Index", "Home");

            return null;
        }
    }
}
