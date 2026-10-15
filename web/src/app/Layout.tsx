import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { AdSlot } from './AdSlot'
import { ThemeToggle } from './ThemeToggle'

/**
 * The page frame (docs/06 §2): banners top and bottom on phones, left and right on desktops, each in a
 * fixed reserved slot, so the play area is never covered and never jumps. `wide` is for the host console's
 * dashboard; other pages keep a reading width.
 */
export function Layout({ children, wide = false }: { children: ReactNode; wide?: boolean }) {
  return (
    <div className="flex min-h-dvh flex-col bg-page text-ink lg:flex-row lg:items-start lg:justify-center lg:gap-4 lg:p-4">
      <div className="flex justify-center lg:hidden">
        <AdSlot position="top" />
      </div>
      <div className="hidden lg:block">
        <AdSlot position="left" />
      </div>
      <main
        className={`flex w-full flex-1 flex-col gap-4 p-4 ${wide ? 'max-w-[1100px]' : 'max-w-3xl'}`}
      >
        <header className="flex items-center justify-between gap-3">
          <Link
            to="/"
            className="flex min-h-12 items-center gap-2 rounded-xl font-display text-xl font-extrabold tracking-tight text-ink focus-visible:outline-4 focus-visible:outline-accent"
          >
            <BrandMark />
            SyncBingo
          </Link>
          <ThemeToggle />
        </header>
        {children}
      </main>
      <div className="hidden lg:block">
        <AdSlot position="right" />
      </div>
      <div className="flex justify-center lg:hidden">
        <AdSlot position="bottom" />
      </div>
    </div>
  )
}

/** The logo: a bingo ball in the accent colour. */
export function BrandMark() {
  return (
    <span
      aria-hidden="true"
      className="inline-flex h-8 w-8 items-center justify-center rounded-full bg-accent font-display text-sm font-extrabold text-accent-ink shadow-[inset_0_0_0_5px_rgb(255_255_255/0.22)]"
    >
      S
    </span>
  )
}

/** The display view's frame (FR-28): no ads and no controls, the whole screen for the board; it follows the screen's own theme. */
export function FullScreen({ children }: { children: ReactNode }) {
  return <main className="min-h-dvh bg-page text-ink">{children}</main>
}
