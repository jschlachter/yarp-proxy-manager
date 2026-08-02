"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeftIcon } from "lucide-react";
import CertificateForm, {
  type CreateCertificatePayload,
  type UpdateCertificatePayload,
} from "@/components/certificates/CertificateForm";
import type { Certificate, ProblemDetails } from "@/types";

interface CertificateDetailClientProps {
  id: string;
  isAdmin: boolean;
}

export default function CertificateDetailClient({ id, isAdmin }: CertificateDetailClientProps) {
  const router = useRouter();
  const [certificate, setCertificate] = useState<Certificate | null | undefined>(undefined);
  const [error, setError] = useState<string | undefined>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    let cancelled = false;
    fetch(`/manage/api/certificates/${encodeURIComponent(id)}`)
      .then(async (response) => {
        if (!response.ok) {
          if (!cancelled) setCertificate(null);
          return;
        }
        const data = (await response.json()) as Certificate;
        if (!cancelled) setCertificate(data);
      })
      .catch(() => {
        if (!cancelled) setCertificate(null);
      });
    return () => {
      cancelled = true;
    };
  }, [id]);

  useEffect(() => {
    if (certificate === null) {
      router.replace("/certificates");
    }
  }, [certificate, router]);

  async function handleSubmit(payload: CreateCertificatePayload | UpdateCertificatePayload) {
    if ("certificateFile" in payload) return;

    setError(undefined);
    setIsSubmitting(true);
    try {
      const response = await fetch(`/manage/api/certificates/${encodeURIComponent(id)}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      });

      if (!response.ok) {
        const problem = (await response.json()) as ProblemDetails;
        throw new Error(problem.detail || "Failed to update certificate");
      }

      const updated = (await response.json()) as Certificate;
      setCertificate(updated);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update certificate");
    } finally {
      setIsSubmitting(false);
    }
  }

  if (!certificate) return null;

  return (
    <div className="space-y-6">
      <Link
        href="/certificates"
        className="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ArrowLeftIcon className="h-4 w-4" />
        Back to certificates
      </Link>
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight text-gradient">{certificate.name}</h1>
        <p className="text-sm text-muted-foreground font-mono">
          {certificate.certificateFileName}
        </p>
      </div>

      <div className="rounded-xl border border-border bg-card/80 p-6 backdrop-blur-sm">
        <CertificateForm
          initialData={certificate}
          onSubmit={handleSubmit}
          readOnly={!isAdmin}
          submitLabel="Save Changes"
          error={error}
          isSubmitting={isSubmitting}
        />
      </div>
    </div>
  );
}
