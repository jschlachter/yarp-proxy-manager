import { Skeleton } from "@/components/ui/skeleton";

export default function RoutesLoading() {
  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <Skeleton className="h-7 w-36" />
        <Skeleton className="h-9 w-28" />
      </div>
      <div className="space-y-2">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="flex items-start justify-between rounded-lg border p-4">
            <div className="space-y-2 flex-1">
              <Skeleton className="h-5 w-48" />
              <Skeleton className="h-4 w-64" />
              <Skeleton className="h-4 w-32" />
            </div>
            <div className="ml-4 flex gap-2">
              <Skeleton className="h-8 w-14" />
              <Skeleton className="h-8 w-16" />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
