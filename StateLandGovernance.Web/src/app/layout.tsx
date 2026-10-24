
import type { Metadata } from "next";
import { SYSTEM_TITLE } from "@/shared/constants/navigation";
import "./globals.css";

export const metadata: Metadata = {
  title: SYSTEM_TITLE,
  description:
    "State Land Lease Information and Management System — Component 1 Land Intelligence frontend",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      {/* Grammar extensions can add attributes before hydration. Limit tolerance to
          this element; descendant hydration mismatches must still be reported. */}
      <body suppressHydrationWarning>{children}</body>
    </html>
  );
}
