import { inPractice } from "./practice";
import { expireSession } from "./session";

export class ApiError extends Error {
    constructor(
        readonly status: number,
        message: string
    ) {
        super(message);
    }
}

const messages: Record<string, string> = {
    GROUP_CHAT_FORBIDDEN: "Писать могут участники и организаторы сбора.",
    USER_NOT_FOUND: "Профиль не найден. Проверьте ссылку или ID.",
    INSUFFICIENT_BALANCE: "Недостаточно средств. Пополните кошелёк в личном кабинете.",
    QUANTITY_INVALID: "Проверьте количество товара и единицу продажи.",
    EMAIL_ALREADY_EXISTS: "Эта почта уже зарегистрирована. Войдите в аккаунт.",
    EMAIL_INVALID: "Проверьте адрес электронной почты.",
    EMAIL_REQUIRED: "Введите почту.",
    NICKNAME_REQUIRED: "Введите никнейм.",
    INVALID_CREDENTIALS: "Проверьте почту и пароль.",
    GROUP_NOT_FOUND: "Сбор не найден. Проверьте код приглашения.",
    PRODUCT_NOT_FOUND: "Этого товара больше нет в каталоге.",
    GROUP_CLOSED: "Сбор уже завершён. Вы можете выбрать другой.",
    PRODUCT_HAS_NO_GROUP_PRICES: "Для этого товара пока нет групповой цены.",
    PRICE_TIER_UNAVAILABLE: "Выбранная цена больше недоступна. Проверьте условия товара.",
    IDEMPOTENCY_KEY_REUSED: "Условия сбора изменились. Проверьте их и повторите.",
    ALREADY_JOINED: "У вас уже есть оплаченная покупка в этом сборе.",
    LIMIT_MUST_BE_POSITIVE: "Введите сумму больше нуля.",
    PRICE_TIER_REQUIRED: "Выберите цену своей покупки.",
    CANNOT_LIKE_SELF: "Это ваш профиль организатора.",
    DEADLINE_MUST_BE_IN_FUTURE: "Выберите время в будущем.",
    PASSWORD_LENGTH_INVALID: "Длина пароля — от 12 до 128 символов.",
    DELIVERY_ADDRESS_REQUIRED: "Укажите адрес доставки.",
    ADDRESS_CITY_INVALID: "Проверьте населённый пункт.",
    ADDRESS_STREET_INVALID: "Проверьте название улицы.",
    ADDRESS_HOUSE_INVALID: "Проверьте номер дома.",
    ADDRESS_BUILDING_INVALID: "Проверьте корпус или строение.",
    ADDRESS_APARTMENT_INVALID: "Проверьте номер квартиры или офиса.",
    VALIDATION_FAILED: "Проверьте введённые данные."
};

interface Options {
    method?: "GET" | "POST" | "PUT" | "DELETE";
    body?: unknown;
    token?: string;
    signal?: AbortSignal;
    creationKey?: string;
}

export async function api<T>(path: string, options: Options = {}): Promise<T> {
    if (inPractice()) {
        const { practiceRequest } = await import("./practiceData");
        try {
            return practiceRequest(path, options.method, options.body) as T;
        } catch (error) {
            throw new ApiError(400, error instanceof Error ? error.message : "Ошибка учебного режима.");
        }
    }
    const headers = new Headers({ Accept: "application/json" });
    if (options.body !== undefined) headers.set("Content-Type", "application/json");
    if (options.token) headers.set("Authorization", "Bearer " + options.token);
    if (options.creationKey) headers.set("Idempotency-Key", options.creationKey);

    const response = await fetch("/api" + path, {
        method: options.method ?? "GET",
        headers,
        body: options.body === undefined ? undefined : JSON.stringify(options.body),
        signal: options.signal
    });

    if (response.status === 401 && options.token) {
        expireSession(options.token);
        throw new ApiError(401, "Войдите снова, чтобы продолжить.");
    }
    if (!response.ok) {
        const problem = await response.json().catch(() => ({}));
        const codes = Object.values(problem?.errors ?? {}).flat();
        const first = codes.find((code): code is string => typeof code === "string" && !!messages[code]);
        const fallback =
            response.status === 429
                ? "Слишком много попыток. Попробуйте через минуту."
                : "Запрос не удался. Попробуйте ещё раз.";
        throw new ApiError(response.status, messages[first ?? problem?.title] ?? fallback);
    }
    return response.status === 204 ? undefined as T : response.json() as Promise<T>;
}

export function errorMessage(error: unknown) {
    return error instanceof ApiError
        ? error.message
        : "Не удалось связаться с сервером. Проверьте соединение и попробуйте ещё раз.";
}
