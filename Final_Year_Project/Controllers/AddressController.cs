using Final_Year_Project.Enums;
using Final_Year_Project.Models.Address;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using System.Security.Claims;

namespace Final_Year_Project.Controllers
{
    [Route("Profile/Address")]
    public class AddressController : Controller
    {
        private readonly SupabaseService _supabaseService;
        private readonly EnumService _enumService;

        public AddressController(SupabaseService supabaseService, EnumService enumService)
        {
            _supabaseService = supabaseService;
            _enumService = enumService;
        }

        public static readonly Dictionary<string, (int Min, int Max)> MalaysiaPostalRules = new()
        {
            { "Kuala Lumpur",   (50000, 60000) },
            { "Putrajaya",      (62000, 62999) },
            { "Selangor",       (40000, 48999) },
            { "Johor",          (79000, 86999) },
            { "Melaka",         (75000, 78399) },
            { "Negeri Sembilan",(70000, 73599) },
            { "Perak",          (30000, 36899) },
            { "Penang",         (10000, 14499) },
            { "Kedah",          (5000, 98100) },
            { "Perlis",         (1000, 2999) },
            { "Terengganu",     (20000, 24999) },
            { "Pahang",         (25000, 28800) },
            { "Kelantan",       (15000, 18599) },
            { "Sabah",          (88000, 91300) },
            { "Sarawak",        (93000, 98859) }
        };


        [HttpGet("")]
        public async Task<IActionResult> Address()
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue);
            var client = _supabaseService.GetClient();

            var addressResponse = await client
                .From<Models.DB.Address>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uid)
                .Get();

            var addressList = addressResponse.Models.Select(a => new AddressViewModel
            {
                AId = a.AId,
                AddressName = a.AddressName,
                State = a.State,
                PostalCode = a.PostalCode,
                UnitNo = a.UnitNo,
                IsDefault = a.IsDefault,
            }).ToList();

            return View(addressList);
        }

        [HttpGet("AddAddress")]
        public IActionResult AddAddress()
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            ViewBag.Uid = int.Parse(uidValue);
            return View(new AddressAddEditVM());
        }

        [HttpPost("AddAddress")]
        public async Task<IActionResult> AddAddress(AddressAddEditVM model)
        {
            var client = _supabaseService.GetClient();

            var state = _enumService.GetEnumDisplayName(model.State);

            if (MalaysiaPostalRules.ContainsKey(state))
            {
                var range = MalaysiaPostalRules[state];

                if (!int.TryParse(model.PostalCode, out int code) ||
                    code < range.Min || code > range.Max)
                {
                    ModelState.AddModelError("PostalCode",
                        $"Invalid postal code for {state}. Valid range is {range.Min}-{range.Max}.");
                    ViewBag.Error = "Invalid Postal Code.";
                }
            }

            if (!ModelState.IsValid)
            {
                return RedirectToAction("Address");
            }


            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue);

            if (model.IsDefault)
            {
                await client
                    .From<Models.DB.Address>()
                    .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uid)
                    .Set(x => x.IsDefault, false)
                    .Update();
            }

            var newAddress = new Models.DB.Address
            {
                AddressName = model.AddressName,
                State = state,
                PostalCode = model.PostalCode,
                UnitNo = model.UnitNo,
                IsDefault = model.IsDefault,
                UId = uid
            };

            await client.From<Models.DB.Address>().Insert(newAddress);

            return RedirectToAction("Address");
        }

        [HttpPost("EditAddress")]
        public async Task<IActionResult> EditAddress(int id, AddressAddEditVM model)
        {
            var client = _supabaseService.GetClient();
            var state = _enumService.GetEnumDisplayName(model.State);

            if (MalaysiaPostalRules.ContainsKey(state))
            {
                var range = MalaysiaPostalRules[state];

                if (!int.TryParse(model.PostalCode, out int code) ||
                    code < range.Min || code > range.Max)
                {
                    ModelState.AddModelError("PostalCode",
                        $"Invalid postal code for {state}. Valid range is {range.Min}-{range.Max}.");
                    ViewBag.Error = "Invalid Postal Code.";
                }
            }

            if (!ModelState.IsValid)
                return RedirectToAction("Address");

            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue);

            if (model.IsDefault)
            {
                await client
                    .From<Models.DB.Address>()
                    .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uid)
                    .Set(x => x.IsDefault, false)
                    .Update();
            }

            await client
                .From<Models.DB.Address>()
                .Filter("AId", Supabase.Postgrest.Constants.Operator.Equals, id)
                .Set(x => x.AddressName, model.AddressName)
                .Set(x => x.State, state)
                .Set(x => x.PostalCode, model.PostalCode)
                .Set(x => x.UnitNo, model.UnitNo)
                .Set(x => x.IsDefault, model.IsDefault)
                .Update();

            return RedirectToAction("Address");
        }
    }
}
