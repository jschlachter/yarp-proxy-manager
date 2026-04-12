import { Skeleton } from "@/components/ui/skeleton";

export default function RouteDetailLoading() {
  return (
    <div className="p-6 max-w-xl space-y-6">
      <Skeleton className="h-7 w-48" />
      <div className="space-y-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="space-y-1.5">
            <Skeleton className="h-4 w-24" />
            <Skeleton className="h-9 w-full" />
          </div>
        ))}
        <Skeleton className="h-9 w-28" />
      </div>
    </div>
  );
}
