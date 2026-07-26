import Link from "next/link";
import { redirect } from "next/navigation";
import { headers } from "next/headers";
import { ArrowLeftIcon } from "lucide-react";
import { getSession } from "@/lib/auth";
import NewCertificateClient from "./NewCertificateClient";

export default async function NewCertificatePage() {
  const headersList = await headers();
  const session = getSession(headersList);

  if (!session.isAdmin) {
    redirect("/certificates");
  }

  return (
    <div className="mx-auto max-w-2xl p-6 sm:p-8 space-y-6">
      <Link
        href="/certificates"
        className="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ArrowLeftIcon className="h-4 w-4" />
        Back to certificates
      </Link>
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight text-gradient">New Certificate</h1>
        <p className="text-sm text-muted-foreground">
          Upload a certificate to make it available for use on your proxy routes.
        </p>
      </div>
      <div className="rounded-xl border border-border bg-card/80 p-6 backdrop-blur-sm">
        <NewCertificateClient />
      </div>
    </div>
  );
}
