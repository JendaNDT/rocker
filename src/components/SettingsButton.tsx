import Link from "next/link";

const GEAR_ANGLES = [0, 45, 90, 135, 180, 225, 270, 315];

/**
 * Permanent settings button, fixed in the top-right corner of every screen.
 * Opens the Profile screen, which holds the app settings.
 */
export function SettingsButton() {
  return (
    <Link
      href="/profile"
      aria-label="Nastavení"
      className="fixed top-3 right-3 z-20 flex h-11 w-11 items-center justify-center rounded-full border border-neutral-800 bg-neutral-900 text-neutral-300 transition-colors hover:text-amber-500"
    >
      <svg viewBox="0 0 48 48" width="20" height="20" aria-hidden="true">
        <g fill="currentColor">
          {GEAR_ANGLES.map((angle) => (
            <rect
              key={angle}
              x="21"
              y="2.5"
              width="6"
              height="11"
              rx="1.6"
              transform={`rotate(${angle} 24 24)`}
            />
          ))}
          <circle cx="24" cy="24" r="13.5" />
        </g>
        {/* Hole — filled with the button background colour (neutral-900). */}
        <circle cx="24" cy="24" r="5.5" fill="#171717" />
      </svg>
    </Link>
  );
}
