import { headers } from "next/headers";
import { getSession } from "@/lib/auth";
import RouteDetailClient from "./RouteDetailClient";

export default async function RouteDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const headersList = await headers();
  const session = getSession(headersList);

  return (
    <div className="p-6 max-w-xl space-y-6">
      <RouteDetailClient id={id} isAdmin={session.isAdmin} />
    </div>
  );
}
