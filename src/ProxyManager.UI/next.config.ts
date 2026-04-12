import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  poweredByHeader: false,
  output: "standalone",
  serverExternalPackages: [],
  allowedDevOrigins: ["proxy-manager.west94.io"]
};

export default nextConfig;
