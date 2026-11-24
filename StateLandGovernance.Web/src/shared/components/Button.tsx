import Link from "next/link";
import type { ButtonHTMLAttributes, ComponentProps, ReactNode } from "react";
import styles from "./Button.module.css";

type Variant = "primary" | "secondary" | "ghost";

type CommonProps = {
  variant?: Variant;
  children: ReactNode;
  className?: string;
};

type ButtonProps = CommonProps &
  ButtonHTMLAttributes<HTMLButtonElement> & { href?: undefined };

type LinkButtonProps = CommonProps &
  Pick<ComponentProps<typeof Link>, "href" | "onClick"> & {
    href: string;
  };

export function Button(props: ButtonProps | LinkButtonProps) {
  const { variant = "primary", children, className = "" } = props;
  const classes = `${styles.button} ${styles[variant]} ${className}`.trim();

  if ("href" in props && props.href) {
    const { href, onClick } = props;
    return (
      <Link href={href} className={classes} onClick={onClick}>
        {children}
      </Link>
    );
  }

  const { href: _href, type = "button", ...buttonProps } = props as ButtonProps;
  return (
    <button type={type} className={classes} {...buttonProps}>
      {children}
    </button>
  );
}
