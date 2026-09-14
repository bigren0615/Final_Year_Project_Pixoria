/**
 * Form Update Helper
 * Use this to automatically handle form submissions with cache invalidation
 * 
 * Usage:
 * FormHelper.onSubmit('#update-post-form', {
 *     invalidateCache: 'posts',
 *     refreshPage: true,
 *     successMessage: 'Post updated!'
 * });
 */

const FormHelper = {
    /**
     * Setup form submission with cache invalidation
     * @param {string} formSelector - CSS selector for form
     * @param {object} options - Configuration
     */
    onSubmit(formSelector, options = {}) {
        const form = document.querySelector(formSelector);
        if (!form) {
            console.warn(`[FormHelper] Form not found: ${formSelector}`);
            return;
        }

        const {
            invalidateCache = null,
            refreshPage = false,
            successMessage = 'Updated successfully!',
            errorMessage = 'Failed to update. Please try again.',
            onSuccess = null,
            onError = null
        } = options;

        form.addEventListener('submit', async (e) => {
            e.preventDefault();

            const submitBtn = form.querySelector('[type="submit"]');
            const originalText = submitBtn?.textContent;
            
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.textContent = 'Saving...';
            }

            try {
                const formData = new FormData(form);
                const response = await fetch(form.action, {
                    method: form.method || 'POST',
                    body: formData,
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                if (response.ok) {
                    // Invalidate cache if specified
                    if (invalidateCache && window.AppCache) {
                        AppCache.invalidate(invalidateCache);
                        console.log(`[FormHelper] Invalidated cache: ${invalidateCache}`);
                    }

                    // Show success message
                    if (successMessage) {
                        this.showNotification(successMessage, 'success');
                    }

                    // Refresh page if needed
                    if (refreshPage) {
                        setTimeout(() => {
                            window.location.reload();
                        }, 500);
                    }

                    // Custom callback
                    if (onSuccess) {
                        await onSuccess(response);
                    }
                } else {
                    throw new Error('Server error');
                }
            } catch (error) {
                console.error('[FormHelper] Error:', error);
                
                if (errorMessage) {
                    this.showNotification(errorMessage, 'error');
                }

                if (onError) {
                    onError(error);
                }
            } finally {
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.textContent = originalText;
                }
            }
        });

        console.log(`[FormHelper] Setup for: ${formSelector}`);
    },

    /**
     * Show notification message
     */
    showNotification(message, type = 'info') {
        const div = document.createElement('div');
        div.className = `alert alert-${type} fixed top-4 right-4 z-50`;
        div.textContent = message;
        div.style.minWidth = '300px';
        
        document.body.appendChild(div);
        
        setTimeout(() => {
            div.remove();
        }, 3000);
    }
};

// Export for use
window.FormHelper = FormHelper;
