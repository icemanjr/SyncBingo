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
}
