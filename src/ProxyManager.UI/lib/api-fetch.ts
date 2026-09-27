import { navigateTo } from "@/lib/navigation";

/**
 * `fetch` for the BFF under `/manage/api`. When the proxy answers 401 (the session could not be
 * refreshed), sends the browser to `/login` so the user signs in again and returns to this page.
 */
export async function apiFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const response = await fetch(input, init);

  if (response.status === 401) {
    const { pathname, search } = window.location;
    navigateTo(`/login?returnUrl=${encodeURIComponent(pathname + search)}`);
  }

  return response;
}
