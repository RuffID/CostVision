export function getRequestVerificationToken(): string | null {
    const element = document.querySelector('meta[name="request-verification-token"]');
    return element ? element.getAttribute('content') : null;
}
