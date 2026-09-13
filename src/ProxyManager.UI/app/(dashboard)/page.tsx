import DashboardSummaryClient from "./DashboardSummaryClient";

export default function DashboardPage() {
  return (
    <div className="mx-auto max-w-5xl p-6 sm:p-8 space-y-8">
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight text-gradient">
          Dashboard
        </h1>
        <p className="text-sm text-muted-foreground">
          Overview of your proxy routes, certificates, and system health.
        </p>
      </div>

      <DashboardSummaryClient />
    </div>
  );
}
