/**
 * Who runs the site and how to reach them, from the build's settings (`web/.env`), so the pages
 * never hard-code a name or an address. Read when used, so tests can stub them.
 */
export const site = {
  get operator() {
    return import.meta.env.VITE_OPERATOR_NAME
  },
  get legalEmail() {
    return import.meta.env.VITE_LEGAL_EMAIL
  },
  get supportEmail() {
    return import.meta.env.VITE_SUPPORT_EMAIL
  },
  /** The site's public address, e.g. https://syncbingo.com: canonical links and the sitemap. */
  get origin() {
    return import.meta.env.VITE_SITE_ORIGIN
  },
  /**
   * The address people type, e.g. syncbingo.com: this site's own in the browser, the public one when the
   * build renders a page ahead of time (there is no browser then).
   */
  get host() {
    return import.meta.env.SSR ? new URL(site.origin).host : window.location.host
  },
}
