import { useId, useState } from "react";
import { addressError, addressFields } from "../address";
import type { DeliveryAddress } from "../types";
import { HelpTip } from "./HelpTip";

export function AddressFields({ value, onChange, disabled, legacy }: {
    value: DeliveryAddress;
    onChange: (value: DeliveryAddress) => void;
    disabled?: boolean;
    legacy?: string | null;
}) {
    const id = useId();
    const [touched, setTouched] = useState<Partial<Record<keyof DeliveryAddress, boolean>>>({});
    const [extras, setExtras] = useState(!!value.apartment || !!value.building);

    function field(key: keyof DeliveryAddress) {
        const entry = addressFields[key];
        const error = addressError(key, value[key]);
        const showError = touched[key] && !!error;
        return <label className="field">
            <span id={id + key + "-label"}>{entry.label}</span>
            <input required={key !== "building" && key !== "apartment"}
                autoComplete={key === "city" ? "address-level2" : key === "street" ? "address-line1" : "off"}
                maxLength={entry.max} placeholder={entry.placeholder} value={value[key] ?? ""}
                aria-labelledby={id + key + "-label"} aria-invalid={showError || undefined}
                aria-describedby={showError ? id + key : undefined}
                ref={input => { input?.setCustomValidity(error); }}
                onChange={event => onChange({ ...value, [key]: event.target.value })}
                onBlur={() => setTouched(previous => ({ ...previous, [key]: true }))}
                onInvalid={() => {
                    if (key === "building" || key === "apartment") setExtras(true);
                    setTouched(previous => ({ ...previous, [key]: true }));
                }} />
            {showError && <small className="field-error" id={id + key}>{error}</small>}
        </label>;
    }

    return <fieldset className="address-fields" disabled={disabled}>
        <legend><span className="with-hint">Адрес доставки <HelpTip label="Как заполнить адрес">
            Укажите населённый пункт, улицу и дом. Корпус и квартиру можно добавить при необходимости. Адрес виден только вам.
        </HelpTip></span></legend>
        {legacy && <p className="field-hint">Прежний адрес · {legacy}</p>}
        <div className="address-grid">{field("city")}{field("street")}{field("house")}</div>
        <details open={extras} onToggle={event => setExtras(event.currentTarget.open)}>
            <summary>Корпус и квартира <small>необязательно</small></summary>
            <div className="address-extras">{field("building")}{field("apartment")}</div>
        </details>
    </fieldset>;
}
