"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { CheckIcon, CopyIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Separator } from "@/components/ui/separator";
import {
  formatRelativeDays,
  getCommonName,
  getExpiryStatus,
  type ExpiryStatus,
} from "@/lib/certificates";
import { cn } from "@/lib/utils";
import type { Certificate } from "@/types";

interface CertificateViewDialogProps {
  certificate: Certificate | undefined;
  onOpenChange: (open: boolean) => void;
}

const STATUS_BADGES: Record<ExpiryStatus, { label: string; className: string }> = {
  valid: { label: "Valid", className: "bg-emerald-500/10 text-emerald-500" },
  expiring: { label: "Expires soon", className: "bg-amber-500/10 text-amber-500" },
  critical: { label: "Critical", className: "bg-orange-500/10 text-orange-500" },
  expired: { label: "Expired", className: "bg-destructive/10 text-destructive" },
};

const chipClassName =
  "rounded-md border border-border/60 bg-muted/60 px-2 py-0.5 text-xs font-mono text-foreground/80 break-words";

export default function CertificateViewDialog({ certificate, onOpenChange }: CertificateViewDialogProps) {
  // Focus the popup itself: the default (first tabbable, the Copy button) scrolls long SAN lists away.
  const popupRef = useRef<HTMLDivElement>(null);
  // Keep rendering the last certificate after close so the exit animation has content to fade out.
  const [shown, setShown] = useState(certificate);
  if (certificate && certificate !== shown) setShown(certificate);

  return (
    <Dialog open={!!certificate} onOpenChange={onOpenChange}>
      {shown && (
        <DialogContent ref={popupRef} initialFocus={popupRef} className="sm:max-w-lg max-h-[85vh] overflow-y-auto">
          <CertificateDetails certificate={shown} />
        </DialogContent>
      )}
    </Dialog>
  );
}

function CertificateDetails({ certificate }: { certificate: Certificate }) {
  const status = getExpiryStatus(certificate.notAfter);
  const isLetsEncrypt = certificate.source === "LetsEncrypt";

  return (
    <>
      <DialogHeader className="pr-8">
        <DialogTitle className="truncate">{certificate.name}</DialogTitle>
        <DialogDescription className="flex flex-wrap gap-1.5">
          <Badge variant="outline" className="border-transparent bg-primary/10 text-primary">
            {certificate.format === "Pfx" ? "PFX" : "PEM"}
          </Badge>
          <Badge
            variant="outline"
            className={cn(
              "border-transparent",
              isLetsEncrypt ? "bg-emerald-500/10 text-emerald-500" : "bg-accent text-accent-foreground"
            )}
          >
            {isLetsEncrypt ? "Let's Encrypt" : "Manual"}
          </Badge>
          <StatusBadge status={status} />
        </DialogDescription>
      </DialogHeader>

      <Section title="Identity">
        <Row label="Common name">{getCommonName(certificate.subject) ?? "—"}</Row>
        <Row label="Subject">
          <span className="break-words">{certificate.subject}</span>
        </Row>
        <Row label="Alternative names">
          {certificate.subjectAlternativeNames.length > 0 ? (
            <div className="flex flex-wrap gap-1.5">
              {certificate.subjectAlternativeNames.map((san) => (
                <span key={san} className={chipClassName}>
                  {san}
                </span>
              ))}
            </div>
          ) : (
            "None"
          )}
        </Row>
      </Section>
      <Separator />
      <Section title="Validity">
        <Row label="Valid from">
          <DateValue date={certificate.notBefore} />
        </Row>
        <Row label="Expires">
          <DateValue date={certificate.notAfter} />{" "}
          <span className="text-muted-foreground">({formatRelativeDays(certificate.notAfter)})</span>
        </Row>
        <Row label="Status">
          <StatusBadge status={status} />
        </Row>
      </Section>
      <Separator />
      <Section title="Files & metadata">
        <Row label="Certificate file">
          <span className={chipClassName}>{certificate.certificateFileName}</span>
        </Row>
        {certificate.keyFileName && (
          <Row label="Key file">
            <span className={chipClassName}>{certificate.keyFileName}</span>
          </Row>
        )}
        <Row label="Thumbprint">
          <Thumbprint value={certificate.thumbprint} />
        </Row>
        <Row label="Uploaded">
          <DateValue date={certificate.createdAt} />
        </Row>
        <Row label="Last updated">
          <DateValue date={certificate.updatedAt} />
        </Row>
      </Section>
    </>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="space-y-2">
      <h3 className="text-xs font-semibold uppercase tracking-wider text-primary">{title}</h3>
      <dl className="grid grid-cols-1 gap-x-4 gap-y-1 sm:grid-cols-[9rem_1fr] sm:gap-y-2">{children}</dl>
    </section>
  );
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <>
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="mb-2 min-w-0 sm:mb-0">{children}</dd>
    </>
  );
}

function StatusBadge({ status }: { status: ExpiryStatus }) {
  const { label, className } = STATUS_BADGES[status];
  return (
    <Badge variant="outline" className={cn("border-transparent", className)}>
      {label}
    </Badge>
  );
}

function DateValue({ date }: { date: string }) {
  return <time dateTime={date} title={date}>{new Date(date).toLocaleString()}</time>;
}

function Thumbprint({ value }: { value: string }) {
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return;
    const timeout = setTimeout(() => setCopied(false), 2000);
    return () => clearTimeout(timeout);
  }, [copied]);

  async function handleCopy() {
    await navigator.clipboard.writeText(value);
    setCopied(true);
  }

  return (
    <div className="flex items-start gap-2">
      <span className="min-w-0 break-all font-mono text-xs leading-6">{value}</span>
      <Button variant="ghost" size="sm" onClick={handleCopy} aria-label="Copy thumbprint" className="shrink-0">
        {copied ? <CheckIcon className="text-emerald-500" /> : <CopyIcon />}
        {copied ? "Copied" : "Copy"}
      </Button>
    </div>
  );
}
