/**
 * Full-page navigation. Kept in its own module so tests can mock it: jsdom's `window.location`
 * can't be redefined or spied on.
 */
export function navigateTo(url: string): void {
  window.location.href = url;
}
