"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { Certificate, CertificateFormat } from "@/types";

export interface CreateCertificatePayload {
  name: string;
  format: CertificateFormat;
  certificateFile: File;
  keyFile?: File;
  passPhrase?: string;
}

export interface UpdateCertificatePayload {
  name: string;
  passPhrase?: string;
}

interface CertificateFormProps {
  initialData?: Certificate;
  onSubmit: (payload: CreateCertificatePayload | UpdateCertificatePayload) => void;
  readOnly?: boolean;
  submitLabel?: string;
  error?: string;
  isSubmitting?: boolean;
  submittingLabel?: string;
}

interface FormErrors {
  name?: string;
  certificateFile?: string;
}

const ACCEPT_BY_FORMAT: Record<CertificateFormat, string> = {
  Pfx: ".pfx,.p12",
  Pem: ".pem,.crt,.cer",
};

const FORMAT_LABEL: Record<CertificateFormat, string> = {
  Pfx: "PFX (PKCS#12, bundled certificate + key)",
  Pem: "PEM (separate certificate and key files)",
};

function isExpired(certificate: Certificate): boolean {
  return new Date(certificate.notAfter).getTime() < Date.now();
}

export default function CertificateForm({
  initialData,
  onSubmit,
  readOnly = false,
  submitLabel = initialData ? "Save Changes" : "Upload Certificate",
  error,
  isSubmitting = false,
  submittingLabel,
}: CertificateFormProps) {
  const [name, setName] = useState(initialData?.name ?? "");
  const [format, setFormat] = useState<CertificateFormat>(initialData?.format ?? "Pfx");
  const [certificateFile, setCertificateFile] = useState<File | null>(null);
  const [keyFile, setKeyFile] = useState<File | null>(null);
  const [passphrase, setPassphrase] = useState("");
  const [errors, setErrors] = useState<FormErrors>({});

  const isEdit = !!initialData;

  function validate(): FormErrors {
    const errs: FormErrors = {};
    if (!name.trim()) errs.name = "Name is required";
    if (!isEdit && !certificateFile) {
      errs.certificateFile = "A certificate file is required";
    }
    return errs;
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const errs = validate();
    if (Object.keys(errs).length > 0) {
      setErrors(errs);
      return;
    }
    setErrors({});

    if (isEdit) {
      onSubmit({
        name: name.trim(),
        passPhrase: passphrase.trim() || undefined,
      });
      return;
    }

    onSubmit({
      name: name.trim(),
      format,
      certificateFile: certificateFile!,
      keyFile: format === "Pem" ? keyFile ?? undefined : undefined,
      passPhrase: passphrase.trim() || undefined,
    });
  }

  function handleFormatChange(next: CertificateFormat) {
    setFormat(next);
    if (next === "Pfx") setKeyFile(null);
  }

  if (readOnly && initialData) {
    return (
      <div className="space-y-4">
        <div>
          <Label>Name</Label>
          <p className="mt-1 text-sm">{initialData.name}</p>
        </div>
        <div>
          <Label>Format</Label>
          <p className="mt-1 text-sm">{initialData.format === "Pfx" ? "PFX" : "PEM"}</p>
        </div>
        <div>
          <Label>Subject</Label>
          <p className="mt-1 text-sm font-mono">{initialData.subject}</p>
        </div>
        {initialData.subjectAlternativeNames.length > 0 && (
          <div>
            <Label>Subject Alternative Names</Label>
            <p className="mt-1 text-sm font-mono">
              {initialData.subjectAlternativeNames.join(", ")}
            </p>
          </div>
        )}
        <div>
          <Label>Certificate File</Label>
          <p className="mt-1 text-sm font-mono">{initialData.certificateFileName}</p>
        </div>
        {initialData.keyFileName && (
          <div>
            <Label>Key File</Label>
            <p className="mt-1 text-sm font-mono">{initialData.keyFileName}</p>
          </div>
        )}
        <div>
          <Label>Valid</Label>
          <p className={`mt-1 text-sm ${isExpired(initialData) ? "text-destructive" : ""}`}>
            {new Date(initialData.notBefore).toLocaleDateString()} –{" "}
            {new Date(initialData.notAfter).toLocaleDateString()}
            {isExpired(initialData) && " (expired)"}
          </p>
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {error && (
        <div role="alert" className="rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm text-destructive">
          {error}
        </div>
      )}

      {isEdit && initialData && isExpired(initialData) && (
        <div role="alert" className="rounded-md border border-amber-500/50 bg-amber-500/10 p-3 text-sm text-amber-600 dark:text-amber-400">
          This certificate expired on {new Date(initialData.notAfter).toLocaleDateString()}.
        </div>
      )}

      <div className="space-y-1.5">
        <Label htmlFor="name">Name</Label>
        <Input
          id="name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g. Wildcard – *.example.com"
          aria-invalid={!!errors.name}
          aria-describedby={errors.name ? "name-error" : undefined}
        />
        <p className="text-xs text-muted-foreground">
          A short description to help identify this certificate&apos;s purpose
        </p>
        {errors.name && (
          <p id="name-error" role="alert" className="text-sm text-destructive">
            {errors.name}
          </p>
        )}
      </div>

      {!isEdit && (
        <>
          <div className="space-y-1.5">
            <Label htmlFor="format">Format</Label>
            <select
              id="format"
              value={format}
              onChange={(e) => handleFormatChange(e.target.value as CertificateFormat)}
              className="h-8 w-full rounded-lg border border-input bg-transparent px-2.5 py-1 text-base outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 md:text-sm dark:bg-input/30"
            >
              <option value="Pfx">{FORMAT_LABEL.Pfx}</option>
              <option value="Pem">{FORMAT_LABEL.Pem}</option>
            </select>
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="certificateFile">Certificate File</Label>
            <Input
              id="certificateFile"
              type="file"
              accept={ACCEPT_BY_FORMAT[format]}
              onChange={(e) => setCertificateFile(e.target.files?.[0] ?? null)}
              aria-invalid={!!errors.certificateFile}
              aria-describedby={errors.certificateFile ? "certificatefile-error" : undefined}
            />
            {errors.certificateFile && (
              <p id="certificatefile-error" role="alert" className="text-sm text-destructive">
                {errors.certificateFile}
              </p>
            )}
          </div>

          {format === "Pem" && (
            <div className="space-y-1.5">
              <Label htmlFor="keyFile">Key File (optional)</Label>
              <Input
                id="keyFile"
                type="file"
                accept=".key,.pem"
                onChange={(e) => setKeyFile(e.target.files?.[0] ?? null)}
              />
            </div>
          )}
        </>
      )}

      <div className="space-y-1.5">
        <Label htmlFor="passphrase">Passphrase (optional)</Label>
        <Input
          id="passphrase"
          type="password"
          value={passphrase}
          onChange={(e) => setPassphrase(e.target.value)}
          placeholder={isEdit ? "Leave blank to keep existing passphrase" : ""}
          autoComplete="new-password"
        />
        <p className="text-xs text-muted-foreground">
          Required only if the certificate or key file is encrypted
        </p>
      </div>

      <Button type="submit" disabled={isSubmitting}>
        {isSubmitting ? submittingLabel ?? "Working…" : submitLabel}
      </Button>
    </form>
  );
}
