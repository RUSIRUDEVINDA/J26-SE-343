import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  async redirects() {
    return [
      {
        source: "/component-01-land-intelligence",
        destination: "/land-intelligence",
        permanent: true,
      },
      {
        source: "/component-01-land-intelligence/:path*",
        destination: "/land-intelligence/:path*",
        permanent: true,
      },
    ];
  },
};

export default nextConfig;
