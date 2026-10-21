// Fills hidden inputs marked with data-browser-timezone with the browser's IANA time zone.
// Runs on full page loads and after Blazor's enhanced navigation, which does not run inline scripts.
(function () {
    function fillTimeZone() {
        var timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
        document.querySelectorAll('input[data-browser-timezone]').forEach(function (input) {
            input.value = timeZone;
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', fillTimeZone);
    } else {
        fillTimeZone();
    }

    if (window.Blazor) {
        window.Blazor.addEventListener('enhancedload', fillTimeZone);
    } else {
        document.addEventListener('DOMContentLoaded', function () {
            if (window.Blazor) {
                window.Blazor.addEventListener('enhancedload', fillTimeZone);
            }
        });
    }
})();
