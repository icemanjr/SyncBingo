/**
 * A reserved, fixed-size ad slot (CLAUDE.md, docs/06): banners only, never pop-ups, never over the play
 * area. Placeholders until a real ad provider; the space is reserved either way, so nothing shifts.
 */
export function AdSlot({ position }: { position: 'top' | 'bottom' | 'left' | 'right' }) {
  const size =
    position === 'top' || position === 'bottom' ? 'h-[50px] w-[320px]' : 'h-[600px] w-[160px]'
  return (
    <aside
      aria-label="Advertisement"
      data-ad-slot={position}
      className={`${size} flex shrink-0 items-center justify-center border border-dashed border-line text-xs text-ink-faint`}
    >
      Ad
    </aside>
  )
}
