import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: '港口实训平台 · 岸桥与场桥交互原型',
  description: '岸桥与场桥统一的学生端、教师端及全屏驾驶交互原型。',
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
