document.addEventListener('DOMContentLoaded', function () {
    const passwordInput = document.getElementById('password');
    const togglePasswordCheckbox = document.getElementById('toggle-password');

    // Debug: Confirm elements are found
    if (!passwordInput) {
        console.error('Password input element not found!');
        return;
    }
    if (!togglePasswordCheckbox) {
        console.error('Toggle password checkbox not found!');
        return;
    }

    console.log('Authentication script loaded successfully.');

    function togglePasswordVisibility() {
        passwordInput.type = passwordInput.type === 'password' ? 'text' : 'password';
        console.log(`Password visibility toggled: ${passwordInput.type}`);
    }

    togglePasswordCheckbox.addEventListener('change', togglePasswordVisibility);
});