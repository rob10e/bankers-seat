import { useMutation } from "@tanstack/react-query";
import { createSession, joinSession } from "./session-api-service.ts";

export const useCreateSessionMutation = () => {
  return useMutation({ mutationFn: createSession });
};

export const useJoinSessionMutation = () => {
  return useMutation({ mutationFn: joinSession });
};
