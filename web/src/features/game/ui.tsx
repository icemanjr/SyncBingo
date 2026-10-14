import type { ButtonHTMLAttributes, ReactNode } from 'react'

const base =
  'inline-flex min-h-12 min-w-12 items-center justify-center rounded-xl px-5 font-semibold focus-visible:outline-4 focus-visible:outline-offset-2 disabled:cursor-not-allowed disabled:opacity-50'

const looks = {
  primary: 'bg-accent text-accent-ink hover:bg-accent-strong focus-visible:outline-accent',
  secondary:
    'border-2 border-accent text-accent-text hover:bg-accent-soft focus-visible:outline-accent',
  danger: 'border-2 border-danger text-danger hover:bg-danger-soft focus-visible:outline-danger',
  quiet: 'text-ink underline-offset-4 hover:underline focus-visible:outline-ink-muted',
}

/** A button with the 48 px tap target (docs/06). */
export function Button({
  look = 'primary',
  className = '',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { look?: keyof typeof looks }) {
  return <button type="button" className={`${base} ${looks[look]} ${className}`} {...props} />
}

/** A message the server wrote for people, shown where the action was. */
export function ErrorText({ children }: { children: ReactNode }) {
  return (
    <p role="alert" className="text-sm font-medium text-danger">
      {children}
    </p>
  )
}

export function Panel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section aria-label={title} className="rounded-2xl bg-surface p-4 shadow-sm">
      <h2 className="mb-3 text-lg font-semibold">{title}</h2>
      {children}
    </section>
  )
}
