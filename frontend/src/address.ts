import type { DeliveryAddress } from "./types";

export const emptyAddress: DeliveryAddress = { city: "", street: "", house: "" };

const placeName = /^(?=.*\p{L})[\p{L}\p{M}\p{Nd} .,'’"()№/\-‐‑–—]+$/u;
const houseNumber = /^(?=.*[1-9])[0-9]+\p{L}?(?:[-/][0-9]+\p{L}?)*$/u;
const unitNumber = /^[\p{L}\p{Nd}]+(?:[ /-][\p{L}\p{Nd}]+)*$/u;

export const addressFields = {
    city: { label: "Населённый пункт", max: 80, pattern: placeName, error: "Укажите название населённого пункта.", placeholder: "Казань" },
    street: { label: "Улица", max: 120, pattern: placeName, error: "Укажите название улицы.", placeholder: "8 Марта" },
    house: { label: "Дом", max: 24, pattern: houseNumber, error: "Введите номер дома, например 12А или 12/1.", placeholder: "12А" },
    building: { label: "Корпус или строение", max: 20, pattern: unitNumber, error: "Укажите номер или букву корпуса.", placeholder: "2" },
    apartment: { label: "Квартира или офис", max: 20, pattern: unitNumber, error: "Укажите номер или букву помещения.", placeholder: "17" }
} as const;

export function addressError(key: keyof DeliveryAddress, rawValue?: string | null) {
    const value = rawValue?.trim() ?? "";
    const field = addressFields[key];
    if (!value && (key === "apartment" || key === "building")) return "";
    return value.length <= field.max && field.pattern.test(value) ? "" : field.error;
}

export function validAddress(address: DeliveryAddress): DeliveryAddress | null {
    const keys = Object.keys(addressFields) as (keyof DeliveryAddress)[];
    if (keys.some(key => addressError(key, address[key]))) return null;
    return {
        city: address.city.trim(), street: address.street.trim(), house: address.house.trim(),
        building: address.building?.trim() || undefined, apartment: address.apartment?.trim() || undefined
    };
}
