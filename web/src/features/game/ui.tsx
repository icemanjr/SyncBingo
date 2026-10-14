import type { ButtonHTMLAttributes, ReactNode } from 'react'

const base =
  'inline-flex min-h-12 min-w-12 items-center justify-center rounded-xl px-5 font-semibold focus-visible:outline-4 focus-visible:outline-offset-2 disabled:cursor-not-allowed disabled:opacity-50'

const looks = {
  primary: 'bg-indigo-600 text-white hover:bg-indigo-700 focus-visible:outline-indigo-600',
  secondary:
    'border-2 border-indigo-600 text-indigo-700 hover:bg-indigo-50 focus-visible:outline-indigo-600',
  danger: 'border-2 border-red-600 text-red-700 hover:bg-red-50 focus-visible:outline-red-600',
  quiet: 'text-slate-700 underline-offset-4 hover:underline focus-visible:outline-slate-600',
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
    <p role="alert" className="text-sm font-medium text-red-700">
      {children}
    </p>
  )
}

export function Panel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section aria-label={title} className="rounded-2xl bg-white p-4 shadow-sm">
      <h2 className="mb-3 text-lg font-semibold">{title}</h2>
      {children}
    </section>
  )
}
