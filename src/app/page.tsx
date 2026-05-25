import Link from "next/link";
import { Dashboard } from "@/components/dashboard/Dashboard";
import { SCREENS } from "@/lib/screens";

export default function HomePage() {
  const sections = SCREENS.filter((screen) => screen.href !== "/");

  return (
    <div className="mx-auto max-w-3xl p-6">
      <h1 className="text-3xl font-bold tracking-tight">Rocker</h1>
      <p className="mt-1 text-dim">
        Kytarový asistent — tvůj denní přehled.
      </p>

      <div className="mt-6">
        <Dashboard />
      </div>

      <h2 className="mt-8 text-lg font-semibold">Vše v aplikaci</h2>
      <div className="mt-3 grid gap-3 sm:grid-cols-2">
        {sections.map((screen) => (
          <Link
            key={screen.href}
            href={screen.href}
            className="rounded-lg border border-line bg-surface p-4 transition-colors hover:border-accent"
          >
            <div className="font-semibold">{screen.label}</div>
            <div className="mt-1 text-sm text-dim">
              {screen.description}
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
