/**
 * PageLoader - Handles AJAX page loading with caching
 * 
 * Features:
 * - Smart caching using AppCache
 * - Skeleton loader animation
 * - History management
 * - Error handling
 * - Works with any module
 * 
 * Usage:
 * new PageLoader('#content-id', {
 *   onLoad: (html) => {},  // Optional callback
 *   cacheCategory: 'posts'
 * });
 */

class PageLoader {
    constructor(contentSelector, options = {}) {
        this.contentEl = document.querySelector(contentSelector);
        this.options = {
            cacheCategory: options.cacheCategory || 'default',
            onLoad: options.onLoad || null,
            onError: options.onError || null,
            skipCache: options.skipCache || false,
            ...options
        };

        this.isLoading = false;
        this.init();
    }

    init() {
        // Handle all link clicks
        document.addEventListener('click', (e) => {
            const link = e.target.closest('a[data-ajax]');
            if (!link) return;

            e.preventDefault();
            const url = link.getAttribute('href');
            if (url) {
                this.load(url);
            }
        });

        // Browser back/forward
        window.addEventListener('popstate', (e) => {
            if (e.state?.ajax && e.state?.url) {
                this.load(e.state.url, false);
            }
        });

        console.log('[PageLoader] Initialized');
    }

    /**
     * Show skeleton loading animation
     */
    showLoader() {
        const skeleton = `
            <div class="space-y-4 p-4">
                <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                    ${Array(6).fill(0).map(() => `
                        <div class="card bg-base-100 shadow-md">
                            <div class="card-body animate-pulse">
                                <div class="h-6 bg-gray-300 rounded w-3/4 mb-4"></div>
                                <div class="h-48 bg-gray-300 rounded mb-4"></div>
                                <div class="h-4 bg-gray-300 rounded w-full mb-2"></div>
                                <div class="h-4 bg-gray-300 rounded w-5/6"></div>
                            </div>
                        </div>
                    `).join('')}
                </div>
            </div>
        `;
        this.contentEl.innerHTML = skeleton;
    }

    /**
     * Load page content
     * @param {string} url - URL to load
     * @param {boolean} pushState - Whether to push browser history
     * @param {boolean} skipCache - Force fresh load, bypass cache
     */
    async load(url, pushState = true, skipCache = false) {
        if (this.isLoading) return;

        // Check cache first
        if (!skipCache) {
            const cacheKey = `page:${url}`;
            const cached = window.AppCache.get(cacheKey);
            if (cached) {
                console.log(`[PageLoader] Cache HIT: ${url}`);
                this.contentEl.innerHTML = cached;
                if (pushState) {
                    history.pushState({ ajax: true, url }, '', url);
                }
                this.options.onLoad?.(cached);
                this.setupEventListeners();
                return;
            }
        }

        // Fetch from server
        this.isLoading = true;
        this.showLoader();

        try {
            console.log(`[PageLoader] Fetching: ${url}`);
            const response = await fetch(url, {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                cache: 'default'
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const html = await response.text();

            // Check if server returned full page (error case)
            if (/<\s*html|<!doctype|<\s*body/i.test(html)) {
                console.warn('[PageLoader] Server returned full page, redirecting');
                window.location.href = url;
                return;
            }

            // Cache the result
            const cacheKey = `page:${url}`;
            window.AppCache.set(cacheKey, html, this.options.cacheCategory);

            // Update UI with fade transition
            this.contentEl.style.opacity = '0';
            this.contentEl.style.transition = 'opacity 0.3s ease';

            await new Promise(resolve => setTimeout(resolve, 150));

            this.contentEl.innerHTML = html;
            this.contentEl.style.opacity = '1';

            // Push history
            if (pushState) {
                history.pushState({ ajax: true, url }, '', url);
            }

            this.options.onLoad?.(html);
            this.setupEventListeners();

        } catch (error) {
            console.error('[PageLoader] Error:', error);
            this.options.onError?.(error);
            this.contentEl.innerHTML = `<div class="alert alert-error">Failed to load page. <a href="${url}">Click here to refresh</a></div>`;
        } finally {
            this.isLoading = false;
        }
    }

    /**
     * Setup event listeners for pagination, etc
     */
    setupEventListeners() {
        // Pagination links
        const pageLinks = this.contentEl.querySelectorAll('a[data-page]');
        pageLinks.forEach(link => {
            link.addEventListener('click', (e) => {
                e.preventDefault();
                this.load(link.getAttribute('href'));
            });
        });
    }

    /**
     * Clear cache for this loader
     */
    clearCache() {
        window.AppCache.clear(this.options.cacheCategory);
    }

    /**
     * Reload current content, bypassing cache
     */
    async refresh() {
        const currentUrl = window.location.pathname;
        await this.load(currentUrl, false, true);
    }
}

// Export globally
window.PageLoader = PageLoader;
