
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
    <html lang="en" suppressHydrationWarning>
      {/* Browser extensions (e.g. data-qb-installed) may mutate <html> before hydration. */}
      <body suppressHydrationWarning>{children}</body>
    </html>
  );
}
