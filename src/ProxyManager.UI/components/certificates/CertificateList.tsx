"use client";

import Link from "next/link";
import { ShieldCheckIcon } from "lucide-react";
import CertificateCard from "./CertificateCard";
import type { Certificate } from "@/types";

interface CertificateListProps {
  certificates: Certificate[];
  isAdmin: boolean;
  onDelete: (id: string) => void;
}

export default function CertificateList({ certificates, isAdmin, onDelete }: CertificateListProps) {
  if (certificates.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center gap-4 rounded-2xl border border-dashed border-border bg-card/40 py-20 text-center">
        <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-primary/10 text-primary ring-1 ring-primary/20">
          <ShieldCheckIcon className="h-6 w-6" />
        </span>
        <div className="space-y-1">
          <p className="font-medium">No certificates uploaded yet</p>
          <p className="text-sm text-muted-foreground">
            Upload a certificate to secure your proxy routes with TLS.
          </p>
        </div>
        {isAdmin && (
          <Link
            href="/certificates/new"
            className="inline-flex items-center justify-center rounded-lg brand-gradient px-4 py-2 text-sm font-medium text-primary-foreground shadow-lg shadow-primary/25 transition-all hover:-translate-y-0.5"
            aria-label="Add Certificate"
          >
            Add Certificate
          </Link>
        )}
      </div>
    );
  }

  return (
    <div className="space-y-2">
      {certificates.map((certificate) => (
        <CertificateCard
          key={certificate.id}
          certificate={certificate}
          isAdmin={isAdmin}
          onDelete={onDelete}
        />
      ))}
    </div>
  );
}
