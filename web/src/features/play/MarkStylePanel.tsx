import { Button, Panel } from '../game/ui'
import { Mark } from './Mark'
import { colours, shapes, type MarkStyle } from './markStyle'

/** **⚙ My marks** (FR-51): shape, colour and opacity, with a live preview; kept on this device. */
export function MarkStylePanel({
  style,
  onChange,
  onClose,
}: {
  style: MarkStyle
  onChange: (style: MarkStyle) => void
  onClose: () => void
}) {
  return (
    <Panel title="My marks">
      <div className="flex flex-col gap-4">
        <div className="flex items-center gap-4">
          <div
            aria-label="Preview: a marked 17"
            role="img"
            className="relative flex h-16 w-16 items-center justify-center rounded-lg border-2 border-line-soft bg-surface"
          >
            <Mark style={style} />
            <span
              className="relative z-10 text-2xl font-bold"
              style={{ textShadow: '0 0 3px var(--surface), 0 0 3px var(--surface)' }}
            >
              17
            </span>
          </div>
          <p className="text-sm text-ink-muted">
            The number always stays on top, so it stays readable.
          </p>
        </div>

        <fieldset>
          <legend className="mb-1 font-semibold">Shape</legend>
          <div className="flex flex-wrap gap-2">
            {shapes.map((shape) => (
              <label
                key={shape.value}
                className={`flex min-h-12 cursor-pointer items-center gap-2 rounded-xl border-2 px-3 ${style.shape === shape.value ? 'border-accent bg-accent-soft' : 'border-line-soft'}`}
              >
                <input
                  type="radio"
                  name="mark-shape"
                  checked={style.shape === shape.value}
                  onChange={() => {
                    onChange({ ...style, shape: shape.value })
                  }}
                  className="sr-only"
                />
                <span aria-hidden="true">{shape.symbol}</span>
                {shape.label}
              </label>
            ))}
          </div>
        </fieldset>

        <fieldset>
          <legend className="mb-1 font-semibold">Colour</legend>
          <div className="flex flex-wrap gap-2">
            {colours.map((colour) => (
              <label
                key={colour.value}
                className={`flex min-h-12 min-w-12 cursor-pointer items-center justify-center rounded-xl border-2 ${style.colour === colour.value ? 'border-ink ring-2 ring-accent' : 'border-line-soft'}`}
              >
                <input
                  type="radio"
                  name="mark-colour"
                  checked={style.colour === colour.value}
                  onChange={() => {
                    onChange({ ...style, colour: colour.value })
                  }}
                  aria-label={colour.label}
                  className="sr-only"
                />
                <span
                  aria-hidden="true"
                  className="h-7 w-7 rounded-full"
                  style={{ backgroundColor: colour.value }}
                />
              </label>
            ))}
          </div>
        </fieldset>

        <label className="flex flex-col gap-1">
          <span className="font-semibold">Opacity: {Math.round(style.opacity * 100)}%</span>
          <input
            type="range"
            min={20}
            max={80}
            step={5}
            value={Math.round(style.opacity * 100)}
            onChange={(e) => {
              onChange({ ...style, opacity: Number(e.target.value) / 100 })
            }}
            className="min-h-12"
          />
        </label>

        <Button look="secondary" onClick={onClose}>
          Done
        </Button>
      </div>
    </Panel>
  )
}
