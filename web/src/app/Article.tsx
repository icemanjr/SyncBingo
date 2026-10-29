import { useEffect, type ReactNode } from 'react'
import { Link } from 'react-router'

/** Long-form reading pages (the guides, Terms, Privacy): a reading width, headings and lists styled alike. */
export function Article({
  title,
  before,
  children,
}: {
  title: string
  before?: ReactNode
  children: ReactNode
}) {
  // Each page has its own title in the tab and in search results; leaving restores the site's.
  useEffect(() => {
    document.title = `${title} · SyncBingo`
    return () => {
      document.title = 'SyncBingo'
    }
  }, [title])

  return (
    <article className="mx-auto flex w-full max-w-2xl flex-col gap-4 pb-6 leading-relaxed [&_h2]:mt-4 [&_h2]:font-display [&_h2]:text-2xl [&_h2]:font-bold [&_h3]:mt-2 [&_h3]:font-display [&_h3]:text-lg [&_h3]:font-bold [&_li]:ml-5 [&_li]:list-disc [&_a]:font-semibold [&_a]:text-accent-text [&_a]:underline">
      {before}
      <h1 className="font-display text-3xl font-extrabold tracking-tight">{title}</h1>
      {children}
      <p>
        <Link to="/">Back to SyncBingo</Link>
      </p>
    </article>
  )
}
