import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import type { Certificate } from "@/types";

interface CertificateCardProps {
  certificate: Certificate;
  isAdmin: boolean;
  onDelete: (id: string) => void;
}

export default function CertificateCard({ certificate, isAdmin, onDelete }: CertificateCardProps) {
  const expired = new Date(certificate.notAfter).getTime() < Date.now();

  return (
    <div className="group relative flex items-start justify-between gap-4 overflow-hidden rounded-xl border border-border bg-card/80 p-4 backdrop-blur-sm transition-all hover:border-primary/40 hover:shadow-lg hover:shadow-primary/5 hover:-translate-y-0.5">
      <span className="absolute inset-y-0 left-0 w-1 brand-gradient opacity-100" />
      <div className="min-w-0 flex-1 space-y-2 pl-2">
        <div className="flex items-center gap-2.5">
          <span className="font-semibold tracking-tight truncate">
            {certificate.name}
          </span>
          <Badge variant="outline" className="gap-1.5 border-transparent bg-primary/10 text-primary">
            {certificate.format === "Pfx" ? "PFX" : "PEM"}
          </Badge>
          {expired && (
            <Badge variant="outline" className="border-transparent bg-destructive/10 text-destructive">
              Expired
            </Badge>
          )}
        </div>
        <div className="flex flex-wrap gap-1.5">
          <span className="rounded-md border border-border/60 bg-muted/60 px-2 py-0.5 text-xs font-mono text-foreground/80">
            {certificate.certificateFileName}
          </span>
          {certificate.keyFileName && (
            <span className="rounded-md border border-border/60 bg-muted/60 px-2 py-0.5 text-xs font-mono text-foreground/80">
              {certificate.keyFileName}
            </span>
          )}
        </div>
        <p className="text-xs text-muted-foreground">
          Valid until {new Date(certificate.notAfter).toLocaleDateString()}
        </p>
      </div>
      {isAdmin && (
        <div className="flex shrink-0 gap-2 opacity-70 transition-opacity group-hover:opacity-100">
          <Link
            href={`/certificates/${certificate.id}`}
            className={cn(
              "inline-flex items-center justify-center rounded-lg border border-input bg-background/50 px-3 py-1.5 text-sm font-medium transition-colors hover:border-primary/40 hover:bg-accent hover:text-accent-foreground"
            )}
            aria-label="Edit"
          >
            Edit
          </Link>
          <Button
            variant="destructive"
            size="sm"
            onClick={() => onDelete(certificate.id)}
            aria-label="Delete"
          >
            Delete
          </Button>
        </div>
      )}
    </div>
  );
}
