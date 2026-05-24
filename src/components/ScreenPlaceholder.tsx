/**
 * Placeholder for screens whose real content arrives in a later roadmap point.
 */
export function ScreenPlaceholder({
  title,
  description,
}: {
  title: string;
  description: string;
}) {
  return (
    <div className="mx-auto max-w-3xl p-6">
      <h1 className="text-3xl font-bold tracking-tight">{title}</h1>
      <p className="mt-1 text-neutral-400">{description}</p>
      <p className="mt-6 rounded-lg border border-dashed border-neutral-800 p-6 text-sm text-neutral-500">
        Tato obrazovka zatím čeká na obsah — přidá ho další bod roadmapy.
      </p>
    </div>
  );
}
