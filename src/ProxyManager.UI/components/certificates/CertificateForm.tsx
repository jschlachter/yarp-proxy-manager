"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { Certificate, CertificateFormat } from "@/types";
import type { CertificateInput } from "@/lib/certificate-store";

interface CertificateFormProps {
  initialData?: Certificate;
  onSubmit: (payload: CertificateInput) => void;
  readOnly?: boolean;
  submitLabel?: string;
  error?: string;
}

interface FormErrors {
  friendlyName?: string;
  certificateFile?: string;
}

const ACCEPT_BY_FORMAT: Record<CertificateFormat, string> = {
  PFX: ".pfx,.p12",
  PEM: ".pem,.crt,.cer",
};

export default function CertificateForm({
  initialData,
  onSubmit,
  readOnly = false,
  submitLabel = initialData ? "Save Changes" : "Upload Certificate",
  error,
}: CertificateFormProps) {
  const [friendlyName, setFriendlyName] = useState(initialData?.friendlyName ?? "");
  const [format, setFormat] = useState<CertificateFormat>(initialData?.format ?? "PFX");
  const [certificateFile, setCertificateFile] = useState<File | null>(null);
  const [keyFile, setKeyFile] = useState<File | null>(null);
  const [passphrase, setPassphrase] = useState("");
  const [errors, setErrors] = useState<FormErrors>({});
  const [isReplacingFiles, setIsReplacingFiles] = useState(!initialData);

  function validate(): FormErrors {
    const errs: FormErrors = {};
    if (!friendlyName.trim()) errs.friendlyName = "Friendly name is required";
    if (!certificateFile && !initialData?.certificateFileName) {
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
    onSubmit({
      friendlyName: friendlyName.trim(),
      format,
      certificateFileName: certificateFile?.name ?? initialData?.certificateFileName ?? "",
      keyFileName: format === "PEM" ? keyFile?.name ?? initialData?.keyFileName : undefined,
      hasPassphrase: passphrase.trim().length > 0 || (initialData?.hasPassphrase ?? false),
    });
  }

  function handleFormatChange(next: CertificateFormat) {
    setFormat(next);
    if (next === "PFX") setKeyFile(null);
  }

  function handleCancelReplaceFiles() {
    setCertificateFile(null);
    setKeyFile(null);
    setErrors((prev) => ({ ...prev, certificateFile: undefined }));
    setIsReplacingFiles(false);
  }

  if (readOnly && initialData) {
    return (
      <div className="space-y-4">
        <div>
          <Label>Friendly Name</Label>
          <p className="mt-1 text-sm">{initialData.friendlyName}</p>
        </div>
        <div>
          <Label>Format</Label>
          <p className="mt-1 text-sm">{initialData.format}</p>
        </div>
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
          <Label>Passphrase</Label>
          <p className="mt-1 text-sm">{initialData.hasPassphrase ? "Protected" : "None"}</p>
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

      <div className="space-y-1.5">
        <Label htmlFor="friendlyName">Friendly Name</Label>
        <Input
          id="friendlyName"
          value={friendlyName}
          onChange={(e) => setFriendlyName(e.target.value)}
          placeholder="e.g. Wildcard – *.example.com"
          aria-invalid={!!errors.friendlyName}
          aria-describedby={errors.friendlyName ? "friendlyname-error" : undefined}
        />
        <p className="text-xs text-muted-foreground">
          A short description to help identify this certificate&apos;s purpose
        </p>
        {errors.friendlyName && (
          <p id="friendlyname-error" role="alert" className="text-sm text-destructive">
            {errors.friendlyName}
          </p>
        )}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="format">Format</Label>
        <select
          id="format"
          value={format}
          onChange={(e) => handleFormatChange(e.target.value as CertificateFormat)}
          className="h-8 w-full rounded-lg border border-input bg-transparent px-2.5 py-1 text-base outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 md:text-sm dark:bg-input/30"
        >
          <option value="PFX">PFX (PKCS#12, bundled certificate + key)</option>
          <option value="PEM">PEM (separate certificate and key files)</option>
        </select>
      </div>

      {initialData && !isReplacingFiles ? (
        <div className="space-y-1.5">
          <Label>Certificate Files</Label>
          <div className="flex flex-wrap items-center gap-1.5 rounded-lg border border-input bg-muted/30 px-2.5 py-2">
            <span className="rounded-md border border-border/60 bg-muted/60 px-2 py-0.5 text-xs font-mono text-foreground/80">
              {initialData.certificateFileName}
            </span>
            {initialData.keyFileName && (
              <span className="rounded-md border border-border/60 bg-muted/60 px-2 py-0.5 text-xs font-mono text-foreground/80">
                {initialData.keyFileName}
              </span>
            )}
            <Button
              type="button"
              variant="link"
              size="sm"
              className="ml-auto h-auto p-0 text-xs"
              onClick={() => setIsReplacingFiles(true)}
            >
              Replace files
            </Button>
          </div>
        </div>
      ) : (
        <>
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

          {format === "PEM" && (
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

          {initialData && (
            <Button
              type="button"
              variant="link"
              size="sm"
              className="h-auto p-0 text-xs"
              onClick={handleCancelReplaceFiles}
            >
              Cancel
            </Button>
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
          placeholder={initialData?.hasPassphrase ? "Leave blank to keep existing passphrase" : ""}
          autoComplete="new-password"
        />
        <p className="text-xs text-muted-foreground">
          Required only if the certificate or key file is encrypted
        </p>
      </div>

      <Button type="submit">{submitLabel}</Button>
    </form>
  );
}
