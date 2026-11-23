import { Gift, Plus } from "lucide-react";
import { Link, useLocation } from "react-router-dom";
import { useProduct } from "../data";
import { bonusLabel, groupPath, money, orderedTiers, productPath, unitLabel } from "../format";
import { GroupCard, Prices } from "../components/Purchase";
import { GroupSort } from "../components/GroupSort";
import { useGroupSort } from "../useGroupSort";
import { Back, Empty, LoadError, Loading, ProductPhoto } from "../components/States";
import { usePageTitle } from "../usePageTitle";

export function ProductPage({ id }: { id: string }) {
    const sort = useGroupSort();
    const result = useProduct(id);
    const location = useLocation();
    const sourceGroup = new URLSearchParams(location.search).get("group") ?? location.state?.groupId;
    const back = sourceGroup ?
        <Link className="back-link" to={groupPath(sourceGroup)} state={location.state?.groupOrigin}>Вернуться в сбор</Link> : <Back />;
    usePageTitle(result.data?.name ?? "Товар");
    const product = result.data;
    if (!product)
        return (
            <>
                {back}
                {result.isError ? (
                    <LoadError error={result.error} retry={result.refetch} />
                ) : (
                    <Loading />
                )}
            </>
        );
    const tier = orderedTiers(product.priceTiers).at(-1);
    const groups = sort.order === "likes" ? product.openGroups : [...product.openGroups].sort((a, b) =>
        Date.parse(b.createdAt) - Date.parse(a.createdAt) || b.createdAt.localeCompare(a.createdAt) || a.id.localeCompare(b.id)
    );

    return (
        <>
            {back}
            <article className="detail-card">
                <ProductPhoto url={product.imageUrl} name={product.name} className="detail-photo" />
                <div className="detail-copy">
                    <h1>{product.name}</h1>
                    {product.description && <p className="description">{product.description}</p>}
                    <div className="my-5">
                        <p className="price-caption">{tier ? "Минимальная цена в сборе" : "Обычная цена"}</p>
                        <div className="price-line">
                            <strong>{money(tier?.priceKopecks ?? product.regularPriceKopecks)}<small className="unit-price">/ {unitLabel(product.saleUnit)}</small></strong>
                            {tier && tier.priceKopecks < product.regularPriceKopecks && (
                                <s aria-label="Обычная цена">{money(product.regularPriceKopecks)}</s>
                            )}
                        </div>
                        <Prices tiers={product.priceTiers} unit={product.saleUnit} />
                    </div>
                    <p className="product-bonus"><Gift size={16} /> Подарок организатору при успехе · {bonusLabel(product.organizerBonus)}</p>
                    <div className="flex flex-wrap gap-2">
                        {product.openGroups.length > 0 && (
                            <a className="button button-dark" href="#product-groups">
                                Выбрать сбор
                            </a>
                        )}
                        {tier && (
                            <Link
                                className={
                                    "button " +
                                    (product.openGroups.length ? "button-light" : "button-dark")
                                }
                                data-guide="create" to={productPath(id) + "/create"}
                            >
                                <Plus size={16} />
                                Создать сбор
                            </Link>
                        )}
                    </div>
                </div>
            </article>
            <div className="section-block">
                <section id="product-groups">
                    <div className="section-heading">
                        <h2>Открытые сборы</h2>
                        <GroupSort {...sort} />
                    </div>
                    {groups.length ? (
                        <div className="group-grid group-grid-two">
                            {groups.map(group => (
                                <GroupCard key={group.id} group={group} />
                            ))}
                        </div>
                    ) : (
                        <Empty title="Первый сбор за вами">
                            <p>
                                {tier
                                    ? "Откройте сбор и отправьте приглашение тем, с кем хотите купить этот товар."
                                    : "Для этого товара пока нет групповой цены."}
                            </p>
                        </Empty>
                    )}
                </section>
            </div>
        </>
    );
}
