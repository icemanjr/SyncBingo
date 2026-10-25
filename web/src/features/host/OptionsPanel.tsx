import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { normalizeCode } from '../game/codes'
import type { Game } from '../game/gameEvents'
import { idsOf, keyOf, selectionName, type PatternData } from '../game/patterns'
import { settingSections, type SettingInfo, type Settings } from '../game/rules'
import { useAction } from '../game/useAction'
import { Button, ErrorText, Panel } from '../game/ui'
import type { HostApi } from './hostApi'

type Draft = Settings & { winCondition: string }

/** How long "Applied ✓" shows, during which Apply can't be pressed again (docs/06). */
const AppliedForMs = 1500

/**
 * Options (FR-32, docs/06): every rule as a draft, applied in one step with **Apply**, so the host can
 * announce the change while nothing is called. Only changed fields are sent. Hidden in auto mode (the
 * console doesn't offer it); during the countdown Apply waits; the *Between rounds* section is locked
 * during a round. Patterns are the draft card size's (FR-26a: no letters on a smaller card); a grouped set's
 * card size changes in the set editor, with the set, and a 4 × 4 card has no free space to choose.
 */
export function OptionsPanel({
  game,
  settings,
  host,
  onClose,
}: {
  game: Game
  settings: Settings
  host: HostApi
  onClose: () => void
}) {
  const base: Draft = { ...settings, winCondition: game.winCondition }
  const [draft, setDraft] = useState<Draft>(base)
  const [applied, setApplied] = useState(false)
  const [confirmDiscard, setConfirmDiscard] = useState(false)
  const { busy, error, run } = useAction()
  const inRound = game.state === 'Manual' || game.state === 'Auto'
  const counting = game.state === 'Starting'

  const changed = (Object.keys(draft) as (keyof Draft)[]).filter((key) => draft[key] !== base[key])

  useEffect(() => {
    if (!applied) return
    const timer = setTimeout(() => {
      setApplied(false)
    }, AppliedForMs)
    return () => {
      clearTimeout(timer)
    }
  }, [applied])

  const apply = () =>
    run(async () => {
      const sent: Settings = {}
      for (const key of changed) {
        if (key !== 'winCondition') Object.assign(sent, { [key]: draft[key] })
      }

      await host.applyRules(
        Object.keys(sent).length > 0 ? sent : null,
        changed.includes('winCondition') ? draft.winCondition : null,
      )
      setApplied(true)
    })

  const close = () => {
    if (changed.length > 0 && !applied) setConfirmDiscard(true)
    else onClose()
  }

  // The patterns of the card size in the draft: a smaller card has no letters (FR-26a).
  const patternsFor = (size: Draft['cardSize']): PatternData => ({
    patterns: game.cardSizes.find((s) => s.size === size)?.patterns ?? game.patterns,
    presets: game.presets,
  })
  const patternData = patternsFor(draft.cardSize)

  const set = (key: keyof Draft, value: unknown) => {
    setDraft((d) => {
      const next = { ...d, [key]: value }
      if (key !== 'cardSize') return next
      // Keep the patterns the new card has; with none left, Normal Bingo.
      const data = patternsFor(next.cardSize)
      const kept = idsOf(d.winCondition).filter((id) => data.patterns.some((p) => p.id === id))
      const ids = kept.length > 0 ? kept : (data.presets[0]?.patternIds ?? ['rows'])
      return { ...next, winCondition: keyOf(ids, data) }
    })
  }

  // Patterns (FR-21): several may be selected, at least one always is. The draft holds the selection's key.
  const selected = new Set(idsOf(draft.winCondition))
  const grouped = new Set(game.presets.flatMap((preset) => preset.patternIds))
  const toggle = (ids: string[], on: boolean) => {
    const next = new Set(selected)
    for (const id of ids) {
      if (on) next.add(id)
      else next.delete(id)
    }
    if (next.size > 0) set('winCondition', keyOf(next, patternData))
  }

  /** Why a lobby setting can't be changed here, beyond the round lock: shown in place of its help. */
  const lockedBecause = (key: SettingInfo['key']) =>
    key === 'cardSize' && game.content?.groupItems
      ? 'grouped'
      : key === 'freeSpace' && draft.cardSize === 'FourByFour'
        ? 'noCentre'
        : undefined

  const checkbox = (
    label: string,
    checked: boolean,
    lastOne: boolean,
    onChange: (on: boolean) => void,
    mixed = false,
  ) => (
    <label key={label} className="flex min-h-12 items-center gap-3 rounded-xl px-2">
      <input
        type="checkbox"
        checked={checked}
        disabled={lastOne}
        ref={(box) => {
          if (box) box.indeterminate = mixed
        }}
        onChange={(e) => {
          onChange(e.target.checked)
        }}
        className="h-5 w-5"
      />
      {label}
      {lastOne && <span className="text-sm text-ink-muted">(at least one pattern)</span>}
    </label>
  )

  const patternBox = (id: string, name: string) =>
    checkbox(name, selected.has(id), selected.has(id) && selected.size === 1, (on) => {
      toggle([id], on)
    })

  const field = (info: SettingInfo) => {
    const because = lockedBecause(info.key)
    const locked = (info.betweenRounds === true && inRound) || because !== undefined
    const isChanged = draft[info.key] !== base[info.key]
    const id = `setting-${info.key}`
    const value = draft[info.key]
    return (
      <div
        key={info.key}
        className={`flex flex-col gap-1 rounded-xl p-2 ${isChanged ? 'bg-accent-soft ring-2 ring-accent' : ''}`}
      >
        {info.choices ? (
          <>
            <label htmlFor={id} className="font-medium">
              {info.label}
            </label>
            <select
              id={id}
              value={String(value)}
              disabled={locked}
              onChange={(e) => {
                set(info.key, e.target.value)
              }}
              aria-describedby={`${id}-help`}
              className="min-h-12 rounded-xl border-2 border-line bg-surface px-3"
            >
              {info.choices.map((c) => (
                <option key={c.value} value={c.value}>
                  {c.label}
                </option>
              ))}
            </select>
          </>
        ) : info.key === 'autoCallIntervalSeconds' ? (
          <>
            <label htmlFor={id} className="font-medium">
              {info.label}
            </label>
            <input
              id={id}
              type="number"
              min={3}
              max={120}
              value={Number(value)}
              onChange={(e) => {
                set(info.key, Number(e.target.value))
              }}
              aria-describedby={`${id}-help`}
              className="min-h-12 w-28 rounded-xl border-2 border-line bg-surface px-3"
            />
          </>
        ) : (
          <label className="flex min-h-12 items-center gap-3 font-medium">
            <input
              id={id}
              type="checkbox"
              checked={value === true}
              disabled={locked}
              onChange={(e) => {
                set(info.key, e.target.checked)
              }}
              aria-describedby={`${id}-help`}
              className="h-5 w-5"
            />
            {info.label}
          </label>
        )}
        <p id={`${id}-help`} className="text-sm text-ink-muted">
          {because === 'grouped' ? (
            <>
              This set’s groups are the card’s columns: change the card size in the{' '}
              <Link
                to={`/host/${normalizeCode(game.code)}/set`}
                className="font-semibold text-accent-text underline"
              >
                set editor
              </Link>
              .
            </>
          ) : because === 'noCentre' ? (
            'A 4 × 4 card has no centre square, so no free space.'
          ) : (
            info.help
          )}
        </p>
      </div>
    )
  }

  return (
    <Panel title="Options">
      <div className="flex flex-col gap-5">
        <fieldset
          className={`flex flex-col gap-1 rounded-xl ${
            draft.winCondition !== base.winCondition ? 'bg-accent-soft p-2 ring-2 ring-accent' : ''
          }`}
        >
          <legend className="mb-1 font-semibold">Patterns</legend>
          <p className="text-sm text-ink-muted">
            Any selected pattern wins: {selectionName(draft.winCondition, patternData)}
          </p>
          {game.presets.map((preset) => {
            const all = preset.patternIds.every((id) => selected.has(id))
            const some = preset.patternIds.some((id) => selected.has(id))
            return (
              <div key={preset.id} className="flex flex-col">
                {checkbox(
                  preset.name,
                  all,
                  all && selected.size === preset.patternIds.length,
                  (on) => {
                    toggle(preset.patternIds, on)
                  },
                  some && !all,
                )}
                <div className="ml-8 flex flex-col">
                  {patternData.patterns
                    .filter((pattern) => preset.patternIds.includes(pattern.id))
                    .map((pattern) => patternBox(pattern.id, pattern.name))}
                </div>
              </div>
            )
          })}
          {patternData.patterns
            .filter((pattern) => !grouped.has(pattern.id))
            .map((pattern) => patternBox(pattern.id, pattern.name))}
        </fieldset>

        {settingSections.map((section) => (
          <fieldset key={section.title} className="flex flex-col gap-1">
            <legend className="mb-1 font-semibold">{section.title}</legend>
            {section.title === 'Between rounds' && inRound && (
              <p className="text-sm text-ink-muted">
                Change these in the lobby: they shape a whole round.
              </p>
            )}
            {section.settings.map(field)}
          </fieldset>
        ))}

        {error && <ErrorText>{error}</ErrorText>}
        {counting && (
          <p className="text-sm text-ink-muted">
            The round is about to start. Cancel the countdown to change the rules.
          </p>
        )}

        {confirmDiscard ? (
          <div className="flex flex-wrap items-center gap-2">
            <span>
              Discard {changed.length} change{changed.length > 1 ? 's' : ''}?
            </span>
            <Button look="danger" onClick={onClose}>
              Discard
            </Button>
            <Button
              look="quiet"
              onClick={() => {
                setConfirmDiscard(false)
              }}
            >
              Keep editing
            </Button>
          </div>
        ) : (
          <div className="flex flex-wrap gap-2">
            <Button
              disabled={busy || applied || counting || changed.length === 0}
              onClick={() => void apply()}
            >
              {applied
                ? 'Applied ✓'
                : changed.length === 0
                  ? 'Apply'
                  : `Apply ${String(changed.length)} change${changed.length > 1 ? 's' : ''}`}
            </Button>
            <Button look="quiet" onClick={close}>
              Close
            </Button>
          </div>
        )}
      </div>
    </Panel>
  )
}
