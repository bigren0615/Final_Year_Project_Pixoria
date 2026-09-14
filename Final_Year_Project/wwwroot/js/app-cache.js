/**
 * AppCache - Unified Caching System for Entire Application
 * 
 * Features:
 * - Simple in-memory caching with auto-expiration
 * - Flexible TTL per cache type
 * - Pattern-based cache invalidation
 * - Works across all modules (profile, posts, products, etc)
 * - Browser storage support (localStorage)
 * 
 * Usage:
 * - AppCache.set('key', data, 'profile')
 * - AppCache.get('key')
 * - AppCache.clear('profile')  // Clear by pattern
 * - AppCache.invalidate()  // Called after updates
 */

class AppCache {
    constructor() {
        this.memory = new Map();
        this.timers = new Map();
        this.config = {
            'profile': 2 * 60 * 1000,      // 2 minutes
            'posts': 2 * 60 * 1000,        // 2 minutes
            'products': 2 * 60 * 1000,     // 2 minutes
            'plans': 2 * 60 * 1000,        // 2 minutes
            'commissions': 2 * 60 * 1000,  // 2 minutes
            'default': 5 * 60 * 1000       // 5 minutes
        };
    }

    /**
     * Get cache TTL for a category
     * @param {string} category - Cache category (profile, posts, etc)
     * @returns {number} TTL in milliseconds
     */
    getTTL(category = 'default') {
        return this.config[category] || this.config['default'];
    }

    /**
     * Set cache configuration TTL
     * @param {string} category - Cache category
     * @param {number} ttl - Time to live in milliseconds
     */
    setTTL(category, ttl) {
        this.config[category] = ttl;
    }

    /**
     * Store data in cache
     * @param {string} key - Unique cache key
     * @param {*} data - Data to cache
     * @param {string} category - Cache category for TTL management
     */
    set(key, data, category = 'default') {
        // Clear existing timer
        if (this.timers.has(key)) {
            clearTimeout(this.timers.get(key));
        }

        // Store data
        this.memory.set(key, {
            data,
            category,
            timestamp: Date.now()
        });

        // Auto-expire after TTL
        const ttl = this.getTTL(category);
        const timer = setTimeout(() => {
            this.memory.delete(key);
            this.timers.delete(key);
        }, ttl);

        this.timers.set(key, timer);
    }

    /**
     * Retrieve data from cache
     * @param {string} key - Unique cache key
     * @returns {*} Cached data or null if expired/not found
     */
    get(key) {
        const item = this.memory.get(key);
        if (!item) return null;

        return item.data;
    }

    /**
     * Check if key exists in cache
     * @param {string} key - Unique cache key
     * @returns {boolean} True if exists and valid
     */
    has(key) {
        return this.memory.has(key);
    }

    /**
     * Clear cache by pattern or all
     * @param {string} pattern - Pattern to match (e.g., 'posts', 'products')
     * @param {boolean} exactMatch - If true, clears only exact matches
     */
    clear(pattern = null, exactMatch = false) {
        if (!pattern) {
            // Clear all
            this.memory.forEach((_, key) => {
                clearTimeout(this.timers.get(key));
            });
            this.memory.clear();
            this.timers.clear();
            console.log('[AppCache] Cleared all caches');
            return;
        }

        // Clear by pattern
        let cleared = 0;
        this.memory.forEach((_, key) => {
            const matches = exactMatch ? key === pattern : key.includes(pattern);
            if (matches) {
                clearTimeout(this.timers.get(key));
                this.memory.delete(key);
                this.timers.delete(key);
                cleared++;
            }
        });

        console.log(`[AppCache] Cleared ${cleared} entries matching: ${pattern}`);
    }

    /**
     * Invalidate cache after data update
     * Call this after create/update/delete operations
     * @param {string} section - Section that was updated (posts, products, etc)
     */
    invalidate(section) {
        if (!section) {
            this.clear();
            return;
        }

        this.clear(section);
        
        // Also clear parent/related caches
        if (section === 'posts') {
            this.clear('profile');  // Profile home also shows posts
        } else if (section === 'products') {
            this.clear('profile');
        }

        console.log(`[AppCache] Invalidated: ${section}`);
    }

    /**
     * Get cache statistics (for debugging)
     */
    stats() {
        const stats = {
            totalItems: this.memory.size,
            byCategory: {},
            items: []
        };

        this.memory.forEach((value, key) => {
            const category = value.category || 'unknown';
            stats.byCategory[category] = (stats.byCategory[category] || 0) + 1;
            stats.items.push({
                key,
                category,
                age: Date.now() - value.timestamp + 'ms'
            });
        });

        return stats;
    }

    /**
     * Debug: Log current cache state
     */
    debug() {
        console.group('[AppCache] Cache Statistics');
        console.table(this.stats());
        console.groupEnd();
    }
}

// Global instance
window.AppCache = new AppCache();

// Export for use in other contexts
if (typeof module !== 'undefined' && module.exports) {
    module.exports = AppCache;
}
