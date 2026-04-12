"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import RouteForm from "@/components/routes/RouteForm";
import type { ProxyHost } from "@/types";

type RouteFormPayload = Omit<ProxyHost, "id" | "createdAt" | "updatedAt">;

export default function NewRouteClient() {
  const router = useRouter();
  const [error, setError] = useState<string | undefined>();

  async function handleSubmit(payload: RouteFormPayload) {
    setError(undefined);
    const response = await fetch("/manage/api/routes", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });

    if (response.ok) {
      router.push("/routes");
    } else {
      const problem = await response.json();
      setError(problem.detail ?? "Failed to create route");
    }
  }

  return <RouteForm onSubmit={handleSubmit} submitLabel="Create Route" error={error} />;
}
