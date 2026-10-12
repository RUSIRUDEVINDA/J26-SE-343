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
      <body>
        {children}
      </body>
    </html>
  );
}
