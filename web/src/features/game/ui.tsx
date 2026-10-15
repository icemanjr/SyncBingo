import type { ButtonHTMLAttributes, ReactNode } from 'react'

const base =
  'inline-flex min-h-12 min-w-12 items-center justify-center gap-2 rounded-2xl px-5 font-bold transition-colors focus-visible:outline-4 focus-visible:outline-offset-2 disabled:cursor-not-allowed disabled:opacity-50'

const looks = {
  /** The one main action of a screen: Grab ball, Start Round, Join. */
  primary:
    'bg-accent text-accent-ink shadow-[0_6px_16px_-6px_rgb(85_52_224/0.7)] hover:bg-accent-strong focus-visible:outline-accent',
  /** Other actions: a soft tint, so the main one stands out. */
  secondary: 'bg-accent-soft text-accent-text hover:bg-accent-soft/70 focus-visible:outline-accent',
  /** An action that weighs more than a tint but isn't the main one: Auto mode beside Grab ball. */
  outline:
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
    <section aria-label={title} className="rounded-[20px] bg-surface p-5 shadow-card">
      <h2 className="mb-3 font-display text-xl font-bold">{title}</h2>
      {children}
    </section>
  )
}
