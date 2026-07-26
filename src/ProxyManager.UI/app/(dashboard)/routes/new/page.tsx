import Link from "next/link";
import { redirect } from "next/navigation";
import { headers } from "next/headers";
import { ArrowLeftIcon } from "lucide-react";
import { getSession } from "@/lib/auth";
import NewRouteClient from "./NewRouteClient";

export default async function NewRoutePage() {
  const headersList = await headers();
  const session = getSession(headersList);

  if (!session.isAdmin) {
    redirect("/routes");
  }

  return (
    <div className="mx-auto max-w-2xl p-6 sm:p-8 space-y-6">
      <Link
        href="/routes"
        className="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ArrowLeftIcon className="h-4 w-4" />
        Back to routes
      </Link>
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight text-gradient">New Route</h1>
        <p className="text-sm text-muted-foreground">
          Point one or more domains at an upstream destination.
        </p>
      </div>
      <div className="rounded-xl border border-border bg-card/80 p-6 backdrop-blur-sm">
        <NewRouteClient />
      </div>
    </div>
  );
}
