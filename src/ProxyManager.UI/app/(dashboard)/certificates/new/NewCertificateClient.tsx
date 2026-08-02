"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import CertificateForm, {
  type CreateCertificatePayload,
  type UpdateCertificatePayload,
} from "@/components/certificates/CertificateForm";
import type { FileAsset, ProblemDetails } from "@/types";

async function uploadAsset(file: File): Promise<FileAsset> {
  const form = new FormData();
  form.append("file", file, file.name);

  const response = await fetch("/manage/api/files?assetType=certificate", {
    method: "POST",
    body: form,
  });

  if (!response.ok) {
    const problem = (await response.json()) as ProblemDetails;
    throw new Error(problem.detail || "Failed to upload file");
  }

  return response.json() as Promise<FileAsset>;
}

export default function NewCertificateClient() {
  const router = useRouter();
  const [error, setError] = useState<string | undefined>();
  const [stage, setStage] = useState<"idle" | "uploading" | "creating">("idle");

  async function handleSubmit(payload: CreateCertificatePayload | UpdateCertificatePayload) {
    if (!("certificateFile" in payload)) return;

    setError(undefined);
    try {
      setStage("uploading");
      const certificateAsset = await uploadAsset(payload.certificateFile);
      const keyAsset = payload.keyFile ? await uploadAsset(payload.keyFile) : undefined;

      setStage("creating");
      const response = await fetch("/manage/api/certificates", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: payload.name,
          format: payload.format,
          certificateAssetId: certificateAsset.id,
          keyAssetId: keyAsset?.id,
          passPhrase: payload.passPhrase,
        }),
      });

      if (!response.ok) {
        const problem = (await response.json()) as ProblemDetails;
        throw new Error(problem.detail || "Failed to create certificate");
      }

      router.push("/certificates");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create certificate");
    } finally {
      setStage("idle");
    }
  }

  return (
    <CertificateForm
      onSubmit={handleSubmit}
      submitLabel="Upload Certificate"
      error={error}
      isSubmitting={stage !== "idle"}
      submittingLabel={stage === "uploading" ? "Uploading…" : "Creating…"}
    />
  );
}
