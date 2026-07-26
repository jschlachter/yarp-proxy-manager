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
    <div className="mx-auto max-w-2xl p-6 sm:p-8">
      <RouteDetailClient id={id} isAdmin={session.isAdmin} />
    </div>
  );
}
