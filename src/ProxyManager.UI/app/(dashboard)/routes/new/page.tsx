import { redirect } from "next/navigation";
import { headers } from "next/headers";
import { getSession } from "@/lib/auth";
import NewRouteClient from "./NewRouteClient";

export default async function NewRoutePage() {
  const headersList = await headers();
  const session = getSession(headersList);

  if (!session.isAdmin) {
    redirect("/routes");
  }

  return (
    <div className="p-6 max-w-xl space-y-6">
      <h1 className="text-xl font-semibold">New Route</h1>
      <NewRouteClient />
    </div>
  );
}
