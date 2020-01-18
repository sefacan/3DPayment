// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
$(function () {
    $('[data-toggle="popover"]').popover();

    $('#payment-button').click(function (e) {

        // Fetch form to apply Bootstrap validation
        var form = $(this).parents('form');

        if (form[0].checkValidity() === false) {
            e.preventDefault();
            e.stopPropagation();
        }
        else {
            // Perform ajax submit here
            form.submit();
        }

        form.addClass('was-validated');
    });
});