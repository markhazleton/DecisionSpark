/**
 * DecisionSpecs Admin UI - Client-side JavaScript
 * Handles validation display, dynamic form management, and UX enhancements
 */

// Initialize on DOM ready
document.addEventListener('DOMContentLoaded', function() {
    initializeValidation();
    initializeFormEnhancements();
});

/**
 * Initialize client-side validation display
 */
function initializeValidation() {
    const form = document.getElementById('editForm');
    if (!form) return;

    // Listen for server validation errors and enhance display
    const validationSummary = document.querySelector('[data-valmsg-summary]');
    if (validationSummary) {
        enhanceValidationDisplay();
    }

    // Client-side validation on submit
    form.addEventListener('submit', function(e) {
        if (!validateForm()) {
            e.preventDefault();
            showValidationSummary();
        }
    });
}

/**
 * Enhance validation error display with field-level highlighting
 */
function enhanceValidationDisplay() {
    // Find all validation error messages
    const errorElements = document.querySelectorAll('.field-validation-error, .text-danger');
    
    errorElements.forEach(function(errorElement) {
        const errorText = errorElement.textContent.trim();
        if (!errorText) return;

        // Find the associated input field
        const inputId = errorElement.getAttribute('data-valmsg-for');
        if (inputId) {
            const inputElement = document.getElementById(inputId);
            if (inputElement) {
                // Add error styling to input
                inputElement.classList.add('is-invalid');
                
                // Create or update feedback element
                let feedbackElement = inputElement.parentElement.querySelector('.invalid-feedback');
                if (!feedbackElement) {
                    feedbackElement = document.createElement('div');
                    feedbackElement.className = 'invalid-feedback';
                    inputElement.parentElement.appendChild(feedbackElement);
                }
                feedbackElement.textContent = errorText;

                // Scroll to first error
                if (document.querySelectorAll('.is-invalid').length === 1) {
                    inputElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
            }
        }
    });
}

/**
 * Validate form before submission
 */
function validateForm() {
    let isValid = true;
    const form = document.getElementById('editForm');
    if (!form) return true;

    // Clear previous validation
    document.querySelectorAll('.is-invalid').forEach(el => el.classList.remove('is-invalid'));
    document.querySelectorAll('.invalid-feedback').forEach(el => el.remove());

    // Validate required fields
    const requiredFields = form.querySelectorAll('[required]');
    requiredFields.forEach(function(field) {
        if (!field.value.trim()) {
            markFieldInvalid(field, 'This field is required');
            isValid = false;
        }
    });

    // Validate SpecId format
    const specIdInput = document.getElementById('SpecId');
    if (specIdInput && specIdInput.value) {
        const specIdPattern = /^[a-z0-9-]+$/;
        if (!specIdPattern.test(specIdInput.value)) {
            markFieldInvalid(specIdInput, 'Spec ID must contain only lowercase letters, numbers, and hyphens');
            isValid = false;
        }
    }

    // Validate Version format
    const versionInput = document.getElementById('Version');
    if (versionInput && versionInput.value) {
        const versionPattern = /^\d+\.\d+\.\d+$/;
        if (!versionPattern.test(versionInput.value)) {
            markFieldInvalid(versionInput, 'Version must follow format: major.minor.patch (e.g., 2025.12.1)');
            isValid = false;
        }
    }

    // Validate at least one question
    const questionsContainer = document.getElementById('questionsContainer');
    if (questionsContainer) {
        const questions = questionsContainer.querySelectorAll('.question-editor');
        if (questions.length === 0) {
            showError('At least one question is required');
            isValid = false;
        }
    }

    // Validate at least one outcome
    const outcomesContainer = document.getElementById('outcomesContainer');
    if (outcomesContainer) {
        const outcomes = outcomesContainer.querySelectorAll('.outcome-editor');
        if (outcomes.length === 0) {
            showError('At least one outcome is required');
            isValid = false;
        }
    }

    return isValid;
}

/**
 * Mark a field as invalid with error message
 */
function markFieldInvalid(field, message) {
    field.classList.add('is-invalid');
    
    let feedbackElement = field.parentElement.querySelector('.invalid-feedback');
    if (!feedbackElement) {
        feedbackElement = document.createElement('div');
        feedbackElement.className = 'invalid-feedback';
        field.parentElement.appendChild(feedbackElement);
    }
    feedbackElement.textContent = message;
    feedbackElement.style.display = 'block';
}

/**
 * Show validation summary
 */
function showValidationSummary() {
    const existingSummary = document.querySelector('.validation-summary-errors');
    if (existingSummary) {
        existingSummary.scrollIntoView({ behavior: 'smooth', block: 'center' });
        return;
    }

    // Create validation summary
    const summary = document.createElement('div');
    summary.className = 'alert alert-danger validation-summary-errors';
    summary.innerHTML = '<h5>Please correct the following errors:</h5><ul></ul>';
    
    const errorList = summary.querySelector('ul');
    const errors = document.querySelectorAll('.is-invalid');
    errors.forEach(function(field) {
        const feedback = field.parentElement.querySelector('.invalid-feedback');
        if (feedback) {
            const li = document.createElement('li');
            li.textContent = feedback.textContent;
            errorList.appendChild(li);
        }
    });

    const form = document.getElementById('editForm');
    if (form) {
        form.insertBefore(summary, form.firstChild);
        summary.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
}

/**
 * Show error message
 */
function showError(message) {
    const alert = document.createElement('div');
    alert.className = 'alert alert-danger alert-dismissible fade show';
    alert.setAttribute('role', 'alert');
    alert.innerHTML = `
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    `;
    
    const container = document.querySelector('.container-fluid');
    if (container) {
        container.insertBefore(alert, container.firstChild);
        alert.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
}

/**
 * Initialize form enhancements
 */
function initializeFormEnhancements() {
    // Auto-expand textareas
    document.querySelectorAll('textarea').forEach(function(textarea) {
        textarea.addEventListener('input', function() {
            this.style.height = 'auto';
            this.style.height = (this.scrollHeight) + 'px';
        });
    });

    // Confirm navigation away with unsaved changes
    let formChanged = false;
    const form = document.getElementById('editForm');
    if (form) {
        form.addEventListener('change', function() {
            formChanged = true;
        });

        form.addEventListener('submit', function() {
            formChanged = false;
        });

        window.addEventListener('beforeunload', function(e) {
            if (formChanged) {
                e.preventDefault();
                e.returnValue = '';
                return '';
            }
        });
    }

    // Character counters for limited fields
    document.querySelectorAll('input[maxlength], textarea[maxlength]').forEach(function(field) {
        const maxLength = field.getAttribute('maxlength');
        const counter = document.createElement('small');
        counter.className = 'form-text text-muted character-counter';
        counter.textContent = `0 / ${maxLength} characters`;
        field.parentElement.appendChild(counter);

        field.addEventListener('input', function() {
            const currentLength = this.value.length;
            counter.textContent = `${currentLength} / ${maxLength} characters`;
            
            if (currentLength > maxLength * 0.9) {
                counter.classList.add('text-warning');
            } else {
                counter.classList.remove('text-warning');
            }
        });
    });
}

/**
 * Dynamic question management
 */
function addQuestionOption(questionIndex) {
    const optionsContainer = document.querySelector(`[data-question-index="${questionIndex}"] .options-container`);
    if (!optionsContainer) return;

    const optionIndex = optionsContainer.querySelectorAll('.option-editor').length;
    
    const optionHtml = `
        <div class="option-editor input-group mb-2" data-option-index="${optionIndex}">
            <input type="text" 
                   name="Questions[${questionIndex}].Options[${optionIndex}].OptionId" 
                   class="form-control" 
                   placeholder="Option ID" 
                   required />
            <input type="text" 
                   name="Questions[${questionIndex}].Options[${optionIndex}].Label" 
                   class="form-control" 
                   placeholder="Option label" 
                   required />
            <button type="button" 
                    class="btn btn-outline-danger" 
                    onclick="removeOption(${questionIndex}, ${optionIndex})">
                <i class="bi bi-x"></i>
            </button>
        </div>
    `;
    
    optionsContainer.insertAdjacentHTML('beforeend', optionHtml);
}

function removeOption(questionIndex, optionIndex) {
    const option = document.querySelector(`[data-question-index="${questionIndex}"] [data-option-index="${optionIndex}"]`);
    if (option) {
        option.remove();
    }
}

/**
 * Handle API errors with field-level display
 */
function handleApiError(error, form) {
    if (!error || !error.errors) return;

    Object.keys(error.errors).forEach(function(fieldName) {
        const errorMessages = error.errors[fieldName];
        const field = form.querySelector(`[name="${fieldName}"]`);
        
        if (field && errorMessages.length > 0) {
            markFieldInvalid(field, errorMessages[0]);
        }
    });

    showValidationSummary();
}

/**
 * Export functions for use in views
 */
window.DecisionSpecsAdmin = {
    validateForm: validateForm,
    markFieldInvalid: markFieldInvalid,
    showError: showError,
    handleApiError: handleApiError,
    addQuestionOption: addQuestionOption,
    removeOption: removeOption
};
