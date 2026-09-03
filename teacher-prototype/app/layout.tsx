import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: '港口实训平台 · 教师控制台',
  description: '港口岸桥与场桥实训平台教师端原型。',
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="zh-CN">
      <body>{children}</body>
    </html>
  );
}
