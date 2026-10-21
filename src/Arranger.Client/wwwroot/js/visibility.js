// Tells .NET when the page becomes visible again, e.g. when a phone wakes up, so that it can catch up on changes.
export function watch(dotNetRef) {
    const onChange = () => {
        if (document.visibilityState === 'visible') {
            dotNetRef.invokeMethodAsync('OnVisibleAsync');
        }
    };

    document.addEventListener('visibilitychange', onChange);
    return {
        dispose: () => document.removeEventListener('visibilitychange', onChange)
    };
}
