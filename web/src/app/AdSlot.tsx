import { useEffect, useRef, useSyncExternalStore } from 'react'
import { adConfig, loadAdSense } from './ads'

type Position = 'top' | 'bottom' | 'left' | 'right'

/**
 * A reserved, fixed-size ad slot (CLAUDE.md, docs/06 §2): banners only, never pop-ups, never over the play
 * area. The space is the same whether it holds a placeholder, an ad, or nothing because no ad came, so the
 * card never shifts; `overflow-hidden` keeps anything an ad draws inside its box.
 */
export function AdSlot({ position }: { position: Position }) {
  const tower = position === 'left' || position === 'right'
  const [width, height] = tower ? [160, 600] : [320, 50]
  const ads = adConfig()
  const wide = useWideScreen()
  return (
    <aside
      aria-label="Advertisement"
      data-ad-slot={position}
      style={{ width, height }}
      className={`flex shrink-0 items-center justify-center overflow-hidden ${
        ads.provider === 'placeholder'
          ? 'border border-dashed border-line text-xs text-ink-faint'
          : ''
      }`}
    >
      {ads.provider === 'placeholder' ? (
        'Ad'
      ) : // Only the pair this screen shows (Layout hides the other); an ad asked for a hidden slot is wasted.
      tower === wide ? (
        <AdSenseUnit
          client={ads.client}
          slot={tower ? ads.towerSlot : ads.bannerSlot}
          width={width}
          height={height}
        />
      ) : null}
    </aside>
  )
}

/** Ad units already handed to AdSense; asking twice for one (React's development double effects) is an error. */
const requested = new WeakSet<HTMLElement>()

/**
 * One AdSense ad unit. Every request is tagged for child treatment (`data-tag-for-age-treatment="1"`, ADR
 * 0012): no personalized ads and no remarketing, since classrooms play. Each unit is asked for once, when it
 * appears; ads are never refreshed on a timer, which AdSense forbids anyway.
 */
function AdSenseUnit({
  client,
  slot,
  width,
  height,
}: {
  client: string
  slot: string
  width: number
  height: number
}) {
  const unit = useRef<HTMLModElement>(null)
  useEffect(() => {
    const ins = unit.current
    if (!ins || requested.has(ins)) return
    requested.add(ins)
    loadAdSense(client)
    try {
      ;(window.adsbygoogle ??= []).push({})
    } catch {
      // Blocked or failed: the slot stays empty, at its reserved size.
    }
  }, [client])
  return (
    <ins
      ref={unit}
      className="adsbygoogle"
      style={{ display: 'inline-block', width, height }}
      data-ad-client={client}
      data-ad-slot={slot}
      data-tag-for-age-treatment="1"
    />
  )
}

/** Matches Tailwind's `lg` breakpoint, where Layout swaps the top and bottom banners for the side ones. */
const wideQuery = '(min-width: 1024px)'

function useWideScreen(): boolean {
  return useSyncExternalStore(
    (changed) => {
      const query = window.matchMedia(wideQuery)
      query.addEventListener('change', changed)
      return () => {
        query.removeEventListener('change', changed)
      }
    },
    () => window.matchMedia(wideQuery).matches,
  )
}
