import { useQuery } from "@tanstack/react-query";
import { getTemplateCatalog } from "./template-catalog-service.ts";

export const useTemplateCatalogQuery = () => {
  return useQuery({
    queryKey: ["template-catalog"],
    queryFn: getTemplateCatalog,
    staleTime: 60_000,
  });
};
