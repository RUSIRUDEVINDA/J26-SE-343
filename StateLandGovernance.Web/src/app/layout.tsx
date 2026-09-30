import type { Metadata } from "next";
import { Inter, Playfair_Display } from "next/font/google";
import { SYSTEM_TITLE } from "@/shared/constants/navigation";
import "./globals.css";

const inter = Inter({
  subsets: ["latin"],
  variable: "--font-body",
});

const playfair = Playfair_Display({
  subsets: ["latin"],
  variable: "--font-display",
});

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
      <body className={`${inter.variable} ${playfair.variable}`}>
        {children}
      </body>
    </html>
  );
}
