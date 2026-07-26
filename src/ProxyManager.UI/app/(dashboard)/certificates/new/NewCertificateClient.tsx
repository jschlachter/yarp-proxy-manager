"use client";

import { useRouter } from "next/navigation";
import CertificateForm from "@/components/certificates/CertificateForm";
import { certificateStore, type CertificateInput } from "@/lib/certificate-store";

export default function NewCertificateClient() {
  const router = useRouter();

  function handleSubmit(payload: CertificateInput) {
    certificateStore.create(payload);
    router.push("/certificates");
  }

  return <CertificateForm onSubmit={handleSubmit} submitLabel="Upload Certificate" />;
}
