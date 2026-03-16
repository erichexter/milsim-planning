import * as React from "react"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

const badgeVariants = cva(
  // RP0: DM Mono, 9px, full pill, no shadow
  "inline-flex items-center rounded-full border px-2 py-0.5 font-mono text-[9px] tracking-wide transition-colors focus:outline-none",
  {
    variants: {
      variant: {
        // green — published / confirmed
        default:
          "bg-primary-soft border-primary-border text-primary",
        // neutral — draft / secondary info
        secondary:
          "bg-secondary border-border text-muted-foreground",
        // red — errors
        destructive:
          "bg-destructive/10 border-destructive/30 text-destructive",
        // bare outline
        outline:
          "border-border text-foreground",
        // amber — objectives / warnings
        amber:
          "bg-amber-soft border-amber-border text-accent",
        // blue — BLUFOR / info
        info:
          "bg-info-soft border-info-border text-info",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
)

export interface BadgeProps
  extends React.HTMLAttributes<HTMLDivElement>,
    VariantProps<typeof badgeVariants> {}

function Badge({ className, variant, ...props }: BadgeProps) {
  return (
    <div className={cn(badgeVariants({ variant }), className)} {...props} />
  )
}

export { Badge, badgeVariants }
