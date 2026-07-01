"use client";

import PhoneInput, { type Value } from "react-phone-number-input";
import { Phone } from "lucide-react";

import { Field, FieldLabel } from "@/components/ui/field";

type PhoneNumberFieldProps = {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
};

export function PhoneNumberField({
  id,
  label,
  value,
  onChange,
  disabled,
}: PhoneNumberFieldProps) {
  return (
    <Field className="gap-1.5">
      <FieldLabel htmlFor={id} className="text-sm font-semibold text-[#171a20]">
        {label}
      </FieldLabel>
      <div className="relative">
        <Phone
          className="pointer-events-none absolute left-4 top-1/2 z-10 size-4 -translate-y-1/2 text-[#8a93a0]"
          aria-hidden="true"
        />
        <PhoneInput
          id={id}
          name={id}
          defaultCountry="BR"
          international
          countryCallingCodeEditable={false}
          value={value as Value}
          onChange={(currentValue) => onChange(currentValue ?? "")}
          disabled={disabled}
          autoComplete="tel"
          placeholder="(11) 99999-9999"
          className="auth-phone-input"
          numberInputProps={{
            required: true,
            "aria-label": label,
          }}
        />
      </div>
    </Field>
  );
}
