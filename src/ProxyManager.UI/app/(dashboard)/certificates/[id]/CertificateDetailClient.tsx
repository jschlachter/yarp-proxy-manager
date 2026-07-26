"use client";

import { useEffect, useState, useSyncExternalStore } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeftIcon } from "lucide-react";
import CertificateForm from "@/components/certificates/CertificateForm";
import { certificateStore, type CertificateInput } from "@/lib/certificate-store";

interface CertificateDetailClientProps {
  id: string;
  isAdmin: boolean;
}

export default function CertificateDetailClient({ id, isAdmin }: CertificateDetailClientProps) {
  const router = useRouter();
  const certificates = useSyncExternalStore(
    certificateStore.subscribe,
    certificateStore.getSnapshot,
    certificateStore.getSnapshot
  );
  const certificate = certificates.find((cert) => cert.id === id);
  const [error, setError] = useState<string | undefined>();

  useEffect(() => {
    if (!certificate) {
      router.replace("/certificates");
    }
  }, [certificate, router]);

  function handleSubmit(payload: CertificateInput) {
    setError(undefined);
    const updated = certificateStore.update(id, payload);
    if (!updated) {
      setError("Failed to update certificate");
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
        <h1 className="text-2xl font-bold tracking-tight text-gradient">
          {certificate.friendlyName}
        </h1>
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
        />
      </div>
    </div>
  );
}
