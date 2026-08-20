export function CrtOverlay() {
  return (
    <div className="crt-overlay" aria-hidden="true">
      <div className="crt-overlay__scanlines" />
      <div className="crt-overlay__vignette" />
      <div className="crt-overlay__sweep-track">
        <div className="crt-overlay__sweep" />
      </div>
      <div className="crt-overlay__glow" />
    </div>
  )
}
