import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { AdSlot } from './AdSlot'
import { ThemeToggle } from './ThemeToggle'

/**
 * The page frame (docs/06 §2): banners top and bottom on phones, left and right on desktops, each in a
 * fixed reserved slot, so the play area is never covered and never jumps.
 */
export function Layout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-col bg-page text-ink lg:flex-row lg:items-start lg:justify-center lg:gap-4 lg:p-4">
      <div className="flex justify-center lg:hidden">
        <AdSlot position="top" />
      </div>
      <div className="hidden lg:block">
        <AdSlot position="left" />
      </div>
      <main className="flex w-full max-w-3xl flex-1 flex-col gap-4 p-4">
        <header className="flex items-center justify-between gap-3">
          <Link to="/" className="min-h-12 content-center text-lg font-bold text-ink">
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

/** The display view's frame (FR-28): no ads and no controls, the whole screen for the board; it follows the screen's own theme. */
export function FullScreen({ children }: { children: ReactNode }) {
  return <main className="min-h-dvh bg-page text-ink">{children}</main>
}
