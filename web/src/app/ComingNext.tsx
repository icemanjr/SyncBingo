/** A route whose page arrives in a later pull request (roadmap Phase 1). */
export function ComingNext({ what }: { what: string }) {
  return (
    <div className="flex flex-1 items-center justify-center text-center text-ink-muted">
      <p>{what} is on its way.</p>
    </div>
  )
}
