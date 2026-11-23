import { useId } from "react";
import { quantityError, readQuantity, unitLabel } from "../format";
import type { SaleUnit } from "../types";

export function QuantityField({ unit, value, onChange, disabled }: {
    unit: SaleUnit;
    value: string;
    onChange: (value: string) => void;
    disabled?: boolean;
}) {
    const id = useId();
    const invalid = readQuantity(value, unit) == null;
    return <label className="field">Количество, {unitLabel(unit)}
        <input data-guide="quantity" value={value} required inputMode={unit === "piece" ? "numeric" : "decimal"}
            aria-invalid={invalid || undefined} aria-describedby={invalid ? id : undefined}
            disabled={disabled} onChange={event => onChange(event.target.value)} />
        {invalid && <span id={id} className="field-error">{quantityError(value, unit)}</span>}
    </label>;
}
